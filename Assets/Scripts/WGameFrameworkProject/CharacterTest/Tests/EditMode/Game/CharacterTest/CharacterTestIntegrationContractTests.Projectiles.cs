using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MxFramework.Audio;
using MxFramework.Camera;
using MxFramework.Camera.Unity;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Config;
using MxFramework.Core;
using MxFramework.Core.Math;
using MxFramework.DebugUI;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Input;
using MxFramework.Network;
using MxFramework.Rendering;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Runtime.Unity;
using MxFramework.Settings;
using MxFramework.Settings.Input;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.UI;
using MxFramework.UI.ConfigBridge;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestIntegrationContractTests
    {

[Test]
        public void CharacterTestAbilityCatalogSource_MapsAttackSecondaryToFireballAction()
        {
            AbilityDefinitionRegistry registry = CharacterTestAbilityCatalogSource.CreateRegistry();

            Assert.IsTrue(registry.TryGetByInputIntent(
                CharacterTestAbilityIds.FireballInputIntentId,
                out AbilityDefinitionConfig ability));
            Assert.AreEqual(CharacterTestAbilityIds.FireballAbilityStableId, ability.AbilityStableId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballCooldownFrames, ability.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAbilityIds.FireballCooldownGroupId, ability.Cooldown.GroupId);
            Assert.AreEqual(0, ability.Costs.Count);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballActionId, ability.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballIntentId, ability.ActionBinding.ActionIntentId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id2, ability.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballPresentationProfileId,
                ability.PresentationProfileId);

            Assert.IsTrue(registry.TryGetByInputIntent(
                CharacterTestAbilityIds.FireballBurstInputIntentId,
                out AbilityDefinitionConfig burst));
            Assert.AreEqual(CharacterTestAbilityIds.FireballBurstAbilityStableId, burst.AbilityStableId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballBurstCooldownFrames, burst.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAbilityIds.FireballBurstCooldownGroupId, burst.Cooldown.GroupId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballBurstActionId, burst.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballBurstIntentId, burst.ActionBinding.ActionIntentId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id3, burst.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballBurstPresentationProfileId,
                burst.PresentationProfileId);

            Assert.IsTrue(registry.TryGetByInputIntent(
                CharacterTestAbilityIds.FireballLobInputIntentId,
                out AbilityDefinitionConfig lob));
            Assert.AreEqual(CharacterTestAbilityIds.FireballLobAbilityStableId, lob.AbilityStableId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballLobCooldownFrames, lob.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAbilityIds.FireballLobCooldownGroupId, lob.Cooldown.GroupId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballLobActionId, lob.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballLobIntentId, lob.ActionBinding.ActionIntentId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D411_Id3, lob.Damage.AnalysisCategory);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballBurstPresentationProfileId,
                lob.PresentationProfileId);

            Assert.IsTrue(registry.TryGetByInputIntent(
                CharacterTestAbilityIds.GuardAuraInputIntentId,
                out AbilityDefinitionConfig guardAura));
            Assert.AreEqual(CharacterTestAbilityIds.GuardAuraAbilityStableId, guardAura.AbilityStableId);
            Assert.AreEqual(CharacterTestAbilityIds.GuardAuraCooldownFrames, guardAura.Cooldown.Frames);
            Assert.AreEqual(CharacterTestAbilityIds.GuardAuraCooldownGroupId, guardAura.Cooldown.GroupId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.GuardAuraActionId, guardAura.ActionBinding.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.GuardAuraIntentId, guardAura.ActionBinding.ActionIntentId);
            Assert.IsFalse(guardAura.Damage.HasDamage);
        }

[Test]
        public void CharacterActionRuntime_FireballProjectile_DamagesEnemyAndPublishesPresentationEvent()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AlignTargetWithDefaultProjectileSocketLine(target);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime);

            StartFireballThroughActionSystem(
                system,
                world,
                attacker,
                RuntimeFrame.Zero);

            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballActionId, attacker.Action.ActiveActionId);
            HitResolveResult result = RunUntilCombatResult(
                world,
                system.Runtime,
                motion,
                combat,
                attacker,
                maxFrames: 20,
                startFrame: 0);

            Assert.AreEqual(HitResolveKind.Damage, result.Kind);
            Assert.AreEqual(10, result.Damage);
            Assert.AreEqual(90, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
            DamageTelemetryRow telemetry = combat.DamageTelemetryRows
                .First(row => row.ResultKind == HitResolveKind.Damage);
            Assert.AreEqual(DamageProfileSourceKind.ProjectileImpact, telemetry.SourceKind);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballActionId, telemetry.ActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballProjectileImpactStableEventId, telemetry.StableEventId);
            Assert.AreEqual(0, telemetry.DamageProfileId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id3, telemetry.DamageTypeId);
            Assert.AreEqual("body", telemetry.HitZoneId);
            HitPresentationEvent presentation = combat.HitPresentationEvents.Single();
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballPresentationProfileId,
                presentation.PresentationProfileId);
            Assert.AreEqual(result.HitPointWorld, presentation.DamageEvent.HitResult.HitPointWorld);
            Assert.AreEqual(result.HitNormalWorld, presentation.DamageEvent.HitResult.HitNormalWorld);
            Assert.Greater(presentation.DamageEvent.FinalDamage, 0);
            AssertOnHitDefenseBreakBuff(world, target, result.TraceId);
            Assert.AreEqual(1, combat.OnHitBuffLastSummary.RequestedBuffCount);
        }

[Test]
        public void CharacterActionRuntime_RuneTurret_DeploysThenFiresProjectileAtNearestHostile()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 7f, 180f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            combat.SetVfxRuntime(vfx);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.RuneTurretIntentId,
                RuntimeFrame.Zero), "Rune Turret intent should start. reject=" + attacker.Action.LastRejectReason);

            bool sawDeployProjectile = false;
            bool sawActiveTurret = false;
            bool capturedTurretSnapshot = false;
            bool sawShotProjectile = false;
            bool acceptedHit = false;
            int initialHp = world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp);
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            for (int i = 0; i < 220; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);

                CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                for (int projectileIndex = 0;
                    projectileState != null && projectileIndex < projectileState.ActiveCount;
                    projectileIndex++)
                {
                    CombatProjectileSnapshot projectile = projectileState.ActiveProjectiles[projectileIndex];
                    if (projectile.DefinitionId == CharacterTestFireballProjectileAdapter.RuneTurretDeployDefinitionId)
                    {
                        sawDeployProjectile = true;
                        Assert.IsTrue(projectile.HasTargetPoint);
                        AssertFixVector(0f, 0f, 5f, projectile.TargetPoint);
                    }
                    else if (projectile.DefinitionId == CharacterTestFireballProjectileAdapter.RuneTurretShotDefinitionId)
                    {
                        sawShotProjectile = true;
                    }
                }

                sawActiveTurret |= combat.ActiveRuneTurretCount > 0;
                if (combat.ActiveRuneTurretCount > 0 && !capturedTurretSnapshot)
                {
                    CharacterTestRuneTurretRuntimeState runeTurretState = combat.CreateRuneTurretStateSnapshot();
                    Assert.Greater(runeTurretState.ActiveCount, 0);
                    CharacterTestRuneTurretSnapshot turret = runeTurretState.ActiveTurrets[0];
                    Assert.AreEqual(
                        CharacterTestRuneTurretIds.WarmupFrames,
                        turret.ActivateFrame - turret.CreatedFrame);

                    CharacterTestNetworkRuneTurretRuntimeSnapshot networkSnapshot =
                        CharacterTestNetworkRuneTurretSnapshotCodec.Capture(combat);
                    Assert.AreEqual(runeTurretState.ActiveCount, networkSnapshot.ActiveCount);
                    Assert.AreEqual(turret.NextThinkFrame, networkSnapshot.ActiveTurrets[0].NextThinkFrame);
                    var restoredCombat = new CharacterTestCharacterCombatTraceSystem();
                    restoredCombat.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
                    NetworkSnapshotApplyResult applyResult =
                        CharacterTestNetworkRuneTurretSnapshotCodec.Apply(restoredCombat, networkSnapshot);
                    Assert.IsTrue(applyResult.Success, applyResult.Message);
                    RuntimeHashAccumulator originalHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
                    CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHash(originalHash, runeTurretState);
                    RuntimeHashAccumulator restoredHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
                    CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHash(
                        restoredHash,
                        restoredCombat.CreateRuneTurretStateSnapshot());
                    Assert.AreEqual(originalHash.ToHash(), restoredHash.ToHash());
                    RuntimeHashAccumulator ownedHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
                    CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHash(
                        ownedHash,
                        runeTurretState,
                        turret.CasterEntityId);
                    Assert.AreEqual(originalHash.ToHash(), ownedHash.ToHash());
                    RuntimeHashAccumulator nonOwnedHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
                    CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHashExcludingCaster(
                        nonOwnedHash,
                        runeTurretState,
                        turret.CasterEntityId);
                    Assert.AreNotEqual(originalHash.ToHash(), nonOwnedHash.ToHash());
                    CharacterTestNetworkRuneTurretRuntimeSnapshot changedNetworkSnapshot =
                        networkSnapshot.CloneForRollback();
                    changedNetworkSnapshot.ActiveTurrets[0].TraceId += 1;
                    RuntimeHashAccumulator changedFieldHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
                    CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHash(
                        changedFieldHash,
                        changedNetworkSnapshot.ToState());
                    Assert.AreNotEqual(originalHash.ToHash(), changedFieldHash.ToHash());
                    capturedTurretSnapshot = true;
                }

                acceptedHit |= attacker.LastCombatHitResults.Any(result =>
                    result.IsAcceptedDamage
                    && result.TargetId.Equals(physicsRuntime.GetEntityId(target)));
                if (sawDeployProjectile
                    && sawActiveTurret
                    && capturedTurretSnapshot
                    && sawShotProjectile
                    && acceptedHit)
                {
                    break;
                }
            }

            Assert.IsTrue(sawDeployProjectile, "Rune Turret should throw a deploy carrier projectile.");
            Assert.IsTrue(sawActiveTurret, "Rune Turret should create an active turret after landing.");
            Assert.IsTrue(capturedTurretSnapshot, "Rune Turret active state should be snapshot/hash covered.");
            Assert.IsTrue(sawShotProjectile, "Rune Turret should enqueue a standard shot projectile.");
            Assert.IsTrue(acceptedHit, "Rune Turret shot should resolve accepted hostile damage through HitResolve.");
            Assert.Less(
                world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp),
                initialHp);
        }

[Test]
        public void CharacterActionRuntime_StunBoltProjectile_AppliesStunnedWithoutDirectHpDamage()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AlignTargetWithDefaultProjectileSocketLine(target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.StunBoltIntentId,
                RuntimeFrame.Zero));

            HitResolveResult result = RunUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 90,
                startFrame: 0);

            Assert.AreEqual(HitResolveKind.Damage, result.Kind);
            Assert.AreEqual(0, result.Damage);
            Assert.IsFalse(result.IsAcceptedDamage);
            Assert.AreEqual(
                CharacterTestCharacterAttributeIds.DefaultMaxHp,
                world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
            Assert.AreEqual(1, combat.OnHitBuffLastSummary.ResultCount);
            Assert.AreEqual(0, combat.OnHitBuffLastSummary.AcceptedDamageCount);
            Assert.AreEqual(1, combat.OnHitBuffLastSummary.RequestedBuffCount);
            AssertStunnedBuff(world, target, result.TraceId);

            DamageTelemetryRow telemetry = combat.DamageTelemetryRows
                .First(row => row.StableEventId == CharacterTestCharacterActionConfigSource.StunBoltProjectileImpactStableEventId);
            Assert.AreEqual(DamageProfileSourceKind.ProjectileImpact, telemetry.SourceKind);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.StunBoltActionId, telemetry.ActionId);
            Assert.AreEqual(0, telemetry.DamageProfileId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id11, telemetry.DamageTypeId);
            Assert.AreEqual(0, telemetry.IncomingDamage);
            Assert.AreEqual(0, telemetry.FinalDamage);
            Assert.AreEqual(CharacterTestCharacterAttributeIds.DefaultMaxHp, telemetry.HpBefore);
            Assert.AreEqual(CharacterTestCharacterAttributeIds.DefaultMaxHp, telemetry.HpAfter);
            HitPresentationEvent presentation = combat.HitPresentationEvents.Single();
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.StunBoltPresentationProfileId,
                presentation.PresentationProfileId);
            Assert.AreEqual(0, presentation.DamageEvent.FinalDamage);
        }

[Test]
        public void CharacterActionRuntime_SwordWaveProjectile_PiercesMultipleHostileTargets()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            AlignTargetsWithDefaultProjectileSocketLine(firstTarget, secondTarget);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.SwordWaveIntentId,
                RuntimeFrame.Zero));

            List<HitResolveResult> results = RunUntilCombatResults(
                world,
                runtime,
                motion,
                combat,
                attacker,
                expectedAcceptedDamageCount: 2,
                maxFrames: 80,
                startFrame: 0);

            HitResolveResult[] accepted = results
                .Where(result => result.IsAcceptedDamage)
                .ToArray();
            Assert.AreEqual(2, accepted.Length);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    physicsRuntime.GetEntityId(firstTarget),
                    physicsRuntime.GetEntityId(secondTarget)
                },
                accepted.Select(result => result.TargetId).ToArray());
            Assert.AreEqual(
                CharacterTestCharacterCombatStateComponent.DefaultMaxHealth - accepted[0].Damage,
                firstTarget.CombatState.Health);
            Assert.AreEqual(
                CharacterTestCharacterCombatStateComponent.DefaultMaxHealth - accepted[1].Damage,
                secondTarget.CombatState.Health);

            DamageTelemetryRow[] telemetryRows = combat.DamageTelemetryRows
                .Where(row => row.StableEventId == CharacterTestCharacterActionConfigSource.SwordWaveProjectileImpactStableEventId)
                .ToArray();
            Assert.AreEqual(2, telemetryRows.Length);
            Assert.IsTrue(telemetryRows.All(row => row.SourceKind == DamageProfileSourceKind.ProjectileImpact));
            Assert.IsTrue(telemetryRows.All(row => row.ActionId == CharacterTestCharacterActionConfigSource.SwordWaveActionId));
            Assert.IsTrue(telemetryRows.All(row => row.DamageTypeId == CharacterTestAuthoredContentIds.D409_Id8));

            CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
            Assert.AreEqual(1, projectileState.ActiveCount);
            CombatProjectileSnapshot projectile = projectileState.ActiveProjectiles[0];
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.SwordWaveDefinitionId, projectile.DefinitionId);
            Assert.AreEqual(2, projectile.AcceptedImpactCount);
        }

[Test]
        public void CharacterActionRuntime_SwordWaveProjectile_BlockedByStaticWorldSurface()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            RegisterStaticAabb(
                physicsRuntime.World,
                entity: CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 10,
                body: CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 10,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.One, Fix64.Zero, Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.One, Fix64.FromInt(3), Fix64.FromRatio(24, 10)));
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.SwordWaveIntentId,
                RuntimeFrame.Zero));

            HitResolveResult result = RunUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 80,
                startFrame: 0);

            Assert.AreEqual(HitResolveKind.Blocked, result.Kind);
            Assert.AreEqual(
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 10),
                result.TargetId);
            Assert.AreEqual(0, combat.ActiveProjectileCount);
            Assert.AreEqual(0, attacker.LastCombatHitResults.Count(hit => hit.IsAcceptedDamage));
        }

[Test]
        public void CharacterActionRuntime_DashStrike_DashesForwardAndHitsTargetOnce()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0.95f, 0f));
            int initialZ = attacker.Transform.ZMillimeters;
            var observedTelemetry = new List<DamageTelemetryRow>();

            List<HitResolveResult> results = RunDashStrikeForFrames(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40,
                observedTelemetry: observedTelemetry);

            HitResolveResult[] accepted = results
                .Where(result => result.IsAcceptedDamage)
                .ToArray();
            Assert.AreEqual(1, accepted.Length);
            Assert.AreEqual(physicsRuntime.GetEntityId(target), accepted[0].TargetId);
            Assert.AreEqual(
                CharacterTestCharacterCombatStateComponent.DefaultMaxHealth - accepted[0].Damage,
                target.CombatState.Health);
            Assert.GreaterOrEqual(
                attacker.Transform.ZMillimeters - initialZ,
                3000,
                "The hostile body may shorten root motion through dynamic collision, but the dash must still advance materially.");
            DamageTelemetryRow telemetry = observedTelemetry
                .First(row => row.StableEventId == CharacterTestCharacterActionConfigSource.DashStrikeHitWindowStableEventId);
            Assert.AreEqual(DamageProfileSourceKind.WeaponTrace, telemetry.SourceKind);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.DashStrikeActionId, telemetry.ActionId);
            Assert.AreEqual(0, telemetry.DamageProfileId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id1, telemetry.DamageTypeId);
        }

[Test]
        public void CharacterActionRuntime_DashStrike_CasterBodyRangeFollowsPhysicsRadius()
        {
            Assert.AreEqual(0, RunDashStrikeSideTargetAcceptedHits(attackerRadius: 0.34f));
            Assert.AreEqual(1, RunDashStrikeSideTargetAcceptedHits(attackerRadius: 0.72f));
        }

[Test]
        public void CharacterActionRuntime_DashStrike_CasterBodyRadiusModifiersChangeRange()
        {
            CharacterTestCharacterActionRuntime expandedRuntime = LoadDashStrikeRuntimeWithHitVolume(new JObject
            {
                ["sourceKind"] = "CasterBody",
                ["bodyRadiusScale"] = 2.0f,
                ["bodyRadiusAdd"] = 0.05f
            });

            Assert.AreEqual(0, RunDashStrikeSideTargetAcceptedHits(attackerRadius: 0.34f));
            Assert.AreEqual(1, RunDashStrikeSideTargetAcceptedHits(
                attackerRadius: 0.34f,
                runtime: expandedRuntime));
        }

[Test]
        public void CharacterActionRuntime_DashStrike_CasterBodyOffsetRotatesWithYaw()
        {
            CharacterTestCharacterActionRuntime offsetRuntime = LoadDashStrikeRuntimeWithHitVolume(new JObject
            {
                ["sourceKind"] = "CasterBody",
                ["bodyOffset"] = new JObject
                {
                    ["z"] = 0.9f
                }
            });

            Assert.AreEqual(0, RunDashStrikeSideTargetAcceptedHits(
                attackerRadius: 0.34f,
                targetXMillimeters: 5500,
                targetZMillimeters: 0,
                attackerYawDegrees: 90f));
            Assert.AreEqual(1, RunDashStrikeSideTargetAcceptedHits(
                attackerRadius: 0.34f,
                runtime: offsetRuntime,
                targetXMillimeters: 5500,
                targetZMillimeters: 0,
                attackerYawDegrees: 90f));
        }

[Test]
        public void CharacterActionRuntime_DashStrike_StaticWallStopsDashEarly()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            RegisterStaticAabb(
                physicsRuntime.World,
                entity: CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 20,
                body: CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 20,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromInt(2), Fix64.Zero, Fix64.FromRatio(16, 10)),
                localMax: new FixVector3(Fix64.FromInt(2), Fix64.FromInt(3), Fix64.FromInt(2)));
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            RunDashStrikeForFrames(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);

            Assert.Greater(attacker.Transform.ZMillimeters, 0);
            Assert.Less(attacker.Transform.ZMillimeters, 2200);
            Assert.AreEqual(0, attacker.LastCombatHitResults.Count(result => result.IsAcceptedDamage));
        }

[Test]
        public void CharacterActionRuntime_SwordWaveDirectionalInteraction_PushesMovableOnceAndProjectileContinues()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                    "movable.sword_wave.target",
                    602,
                    "movable-natural-object",
                    massKg: 80f,
                    CombatSurfaceMaterial.Default,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(0.3f, 0.25f, 1.45f))
            });
            CharacterTestMovableNaturalObjectState movable = world.MovableNaturalObjects.Single();
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            var interactionPhysics = new CharacterTestInteractionPhysicsSystem();

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.SwordWaveIntentId,
                RuntimeFrame.Zero));

            int appliedMovableFrameCount = 0;
            int appliedMovableForceCount = 0;
            int appliedMovableNumericId = 0;
            int observedContactPressure = 0;
            int observedContactCount = 0;
            string observedContactPair = string.Empty;
            int observedContactSourceId = 0;
            bool projectileStillActiveAtImpulse = false;
            CharacterTestInteractionEvent firstInteraction = default;
            for (int i = 0; i < 40; i++)
            {
                var frame = new RuntimeFrame(i);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                if (observedContactCount == 0 && movable.ContactCount > 0)
                {
                    observedContactPressure = movable.ContactPressure;
                    observedContactCount = movable.ContactCount;
                    observedContactPair = movable.ContactPair;
                    observedContactSourceId = movable.ContactAId;
                }

                combat.Tick(world, runtime, frame);
                interactionPhysics.Tick(world, physicsRuntime, combat.LastFrameInteractionEvents, frame);

                if (combat.LastFrameInteractionEvents.Count > 0 && (firstInteraction.StableId == 0))
                    firstInteraction = combat.LastFrameInteractionEvents[0];
                if (interactionPhysics.LastSummary.MovableTargetCount <= 0)
                    continue;

                appliedMovableFrameCount++;
                appliedMovableForceCount += interactionPhysics.LastSummary.AppliedForceCount;
                if (appliedMovableNumericId == 0)
                    appliedMovableNumericId = interactionPhysics.LastSummary.LastMovableNumericId;
                projectileStillActiveAtImpulse |= combat.ActiveProjectileCount > 0;
            }

            Assert.AreEqual(1, appliedMovableFrameCount, "The same Sword Wave projectile should push the same movable once.");
            Assert.AreEqual(1, appliedMovableForceCount);
            Assert.IsTrue(projectileStillActiveAtImpulse, "Movable objects must not consume Sword Wave.");
            Assert.AreEqual(CharacterTestInteractionKind.PhysicalField, firstInteraction.Kind);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.SwordWaveProjectileImpactStableEventId,
                firstInteraction.StableId);
            Assert.AreEqual(
                CharacterTestInteractionSourceDomains.CombatProjectileDirectionalField,
                firstInteraction.SourceDomain);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.SwordWaveImpactPower, firstInteraction.Intensity);
            Assert.AreEqual(
                CharacterTestInteractionFieldShape.OrientedCapsule,
                firstInteraction.PhysicalField.Shape);
            Assert.AreEqual(
                CharacterTestInteractionForceMode.DirectionalImpulse,
                firstInteraction.PhysicalField.ForceMode);
            Assert.IsTrue(firstInteraction.PhysicalField.Targets(
                CharacterTestInteractionTargetFilter.DynamicMovableObjects));
            Assert.IsFalse(firstInteraction.PhysicalField.Targets(
                CharacterTestInteractionTargetFilter.DynamicCharacters));
            Assert.AreEqual(602, appliedMovableNumericId);

            if (observedContactCount == 0)
            {
                CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
                CharacterTestMovableNaturalObjectSystem.Tick(
                    world,
                    physicsRuntime,
                    CharacterTestDeterministicUnits.DefaultTickRate,
                    new RuntimeFrame(40));
                observedContactPressure = movable.ContactPressure;
                observedContactCount = movable.ContactCount;
                observedContactPair = movable.ContactPair;
                    observedContactSourceId = movable.ContactAId;
            }

            Assert.AreEqual(0, movable.VelocityXMillimetersPerSecond);
            Assert.Greater(
                movable.VelocityZMillimetersPerSecond,
                0,
                "Sword Wave directional impulse should add forward velocity.");
            Assert.Greater(
                movable.ZMillimeters,
                1450,
                "Sword Wave directional impulse should move the object forward on the next movable tick.");
            Assert.Greater(
                observedContactPressure,
                0,
                $"Sword Wave directional impulse should record contact pressure. "
                + $"vx={movable.VelocityXMillimetersPerSecond}, "
                + $"vz={movable.VelocityZMillimetersPerSecond}, "
                + $"count={observedContactCount}, pair={observedContactPair}");
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.SwordWaveProjectileImpactStableEventId,
                observedContactSourceId);
        }

[Test]
        public void CharacterActionRuntime_FireballProjectile_UsesSpawnProfileSocketOriginAndForwardTargetPoint()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            attacker.SetTransform(new CharacterTestCharacterTransformComponent(2f, 3f, 4f, 90f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.FireballIntentId,
                RuntimeFrame.Zero));

            CombatProjectileRuntimeState state = default;
            for (int i = 0; i < 10; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                runtime.TickEntity(attacker);
                combat.Tick(world, runtime, frame);
                state = combat.CreateProjectileStateSnapshot();
                if (state.ActiveCount > 0)
                    break;
            }

            Assert.AreEqual(1, state.ActiveCount);
            CombatProjectileSnapshot projectile = state.ActiveProjectiles[0];
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.DefinitionId, projectile.DefinitionId);
            Assert.IsTrue(projectile.HasTargetPoint);
            AssertFixVector(2.177284f, 4.468967f, 4.253929f, projectile.Origin);
            AssertFixVector(1f, 0f, 0f, projectile.Direction);
            AssertFixVector(33.677284f, 4.468967f, 4.253929f, projectile.TargetPoint);
            Assert.AreEqual(90, projectile.EffectiveLifetimeFrames);
        }

[Test]
        public void CharacterActionRuntime_FlameLanceProjectile_LockedTargetAimUsesTargetCenter()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(4f, 0f, 8f, 0f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.FlameLanceIntentId,
                RuntimeFrame.Zero));

            CombatProjectileSnapshot projectile = RunUntilProjectileSpawn(world, runtime, combat, maxFrames: 40);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FlameLanceDefinitionId, projectile.DefinitionId);
            Assert.IsTrue(projectile.HasTargetPoint);
            AssertFixVector(4f, 1f, 8f, projectile.TargetPoint);
            Assert.IsTrue((projectile.TargetPoint - projectile.Origin).TryNormalize(out FixVector3 expectedDirection));
            AssertFixVectorNear(expectedDirection, projectile.Direction, toleranceRaw: 16L);
            Assert.AreEqual(
                ExpectedProjectileLifetimeFrames(
                    projectile.Origin,
                    projectile.TargetPoint,
                    Fix64.FromRatio(55, 100)),
                projectile.EffectiveLifetimeFrames);
        }

[Test]
        public void CharacterActionRuntime_FlameLanceProjectile_NoLockedTargetFallsBackForwardRange()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => 0);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.FlameLanceIntentId,
                RuntimeFrame.Zero));

            CombatProjectileSnapshot projectile = RunUntilProjectileSpawn(world, runtime, combat, maxFrames: 40);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FlameLanceDefinitionId, projectile.DefinitionId);
            Assert.IsTrue(projectile.HasTargetPoint);
            AssertFixVector(0f, 0f, 1f, projectile.Direction);
            Assert.AreEqual(projectile.Origin + new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.FromInt(44)), projectile.TargetPoint);
            Assert.AreEqual(80, projectile.EffectiveLifetimeFrames);
        }

[Test]
        public void CharacterActionRuntime_LaserLanceProjectile_LockedTargetAimUsesTargetCenter()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(4f, 0f, 8f, 0f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.LaserLanceIntentId,
                RuntimeFrame.Zero));

            CombatProjectileSnapshot projectile = RunUntilProjectileSpawn(world, runtime, combat, maxFrames: 40);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.LaserLanceDefinitionId, projectile.DefinitionId);
            Assert.IsTrue(projectile.HasTargetPoint);
            AssertFixVector(4f, 1f, 8f, projectile.TargetPoint);
            Assert.IsTrue((projectile.TargetPoint - projectile.Origin).TryNormalize(out FixVector3 expectedDirection));
            AssertFixVectorNear(expectedDirection, projectile.Direction, toleranceRaw: 16L);
            Assert.AreEqual(1, projectile.EffectiveLifetimeFrames);
        }

[TestCase("missing")]
        [TestCase("friendly")]
        [TestCase("dead")]
        [TestCase("out_of_range")]
        public void CharacterActionRuntime_LaserLanceProjectile_InvalidLockedTargetFallsBackForwardRange(string mode)
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: string.Equals(mode, "friendly", StringComparison.Ordinal) ? 1 : 2,
                out attacker,
                out target);
            if (string.Equals(mode, "dead", StringComparison.Ordinal))
                SetCurrentAttribute(world, target, CharacterTestCharacterAttributeIds.Hp, 0);
            if (string.Equals(mode, "out_of_range", StringComparison.Ordinal))
                target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 80f, 0f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() =>
                string.Equals(mode, "missing", StringComparison.Ordinal) ? 0 : target.Id.Value);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.LaserLanceIntentId,
                RuntimeFrame.Zero));

            CombatProjectileSnapshot projectile = RunUntilProjectileSpawn(world, runtime, combat, maxFrames: 40);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.LaserLanceDefinitionId, projectile.DefinitionId);
            Assert.IsTrue(projectile.HasTargetPoint);
            AssertFixVector(0f, 0f, 1f, projectile.Direction);
            Assert.AreEqual(
                projectile.Origin + new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.FromInt(44)),
                projectile.TargetPoint);
            Assert.AreEqual(1, projectile.EffectiveLifetimeFrames);
        }

[Test]
        public void CharacterActionRuntime_ChannelBeamProjectile_CachesAimDirectionAcrossTicks()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(4f, 0f, 8f, 0f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            int lockedTargetId = target.Id.Value;
            combat.SetLockedTargetEntityIdProvider(() => lockedTargetId);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.ChannelBeamIntentId,
                RuntimeFrame.Zero));

            CombatProjectileSnapshot first = RunUntilProjectileSpawn(world, runtime, combat, maxFrames: 40);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.ChannelBeamDefinitionId, first.DefinitionId);
            Assert.IsTrue(first.HasTargetPoint);
            Assert.IsTrue((new FixVector3(Fix64.FromInt(4), Fix64.One, Fix64.FromInt(8)) - first.Origin)
                .TryNormalize(out FixVector3 expectedLockedDirection));
            AssertFixVectorNear(expectedLockedDirection, first.Direction, toleranceRaw: 2L);
            Assert.IsTrue(combat.TryResolveChannelBeamAimDirection(
                attacker.Id.Value,
                first.ActionInstanceId,
                out FixVector3 cachedDirection));
            AssertFixVectorNear(first.Direction, cachedDirection, toleranceRaw: 2L);

            lockedTargetId = 0;
            target.SetTransform(new CharacterTestCharacterTransformComponent(-6f, 0f, 10f, 0f));
            CombatProjectileSnapshot second = default;
            bool foundSecondTick = false;
            for (int frameValue = first.SpawnFrame.Value + 1; frameValue <= first.SpawnFrame.Value + 12; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);
                combat.Tick(world, runtime, frame);
                CombatProjectileRuntimeState state = combat.CreateProjectileStateSnapshot();
                if (state == null || state.ActiveCount == 0)
                    continue;

                CombatProjectileSnapshot candidate = state.ActiveProjectiles[0];
                if (candidate.DefinitionId != CharacterTestFireballProjectileAdapter.ChannelBeamDefinitionId
                    || candidate.SpawnFrame.Value == first.SpawnFrame.Value)
                    continue;

                second = candidate;
                foundSecondTick = true;
                break;
            }

            Assert.IsTrue(foundSecondTick, "Expected the second Channel Beam tick projectile.");
            Assert.AreEqual(first.ActionInstanceId, second.ActionInstanceId);
            AssertFixVectorNear(first.Direction, second.Direction, toleranceRaw: 2L);
            AssertFixVectorNear(
                second.Origin + second.Direction * Fix64.FromInt(32),
                second.TargetPoint,
                toleranceRaw: 64L);
        }

[Test]
        public void CharacterActionRuntime_ChannelBeamProjectile_RepeatsTickDamageWithoutStasis()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(4f, 0f, 8f, 0f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.ChannelBeamIntentId,
                RuntimeFrame.Zero));

            var results = new List<HitResolveResult>();
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            for (int i = 0; i < 50; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
                if (attacker.LastCombatHitResults.Count > 0)
                    results.AddRange(attacker.LastCombatHitResults);
                if (results.Count(result => result.IsAcceptedDamage) >= 2)
                    break;
            }

            Assert.AreEqual(2, results.Count(result => result.IsAcceptedDamage));
            Assert.AreEqual(1, results.Select(result => result.TargetId.Value).Distinct().Count());
            Assert.Less(
                world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp),
                CharacterTestCharacterAttributeIds.DefaultMaxHp);
            AssertNoStasisBuff(world, target);
            Assert.AreEqual(0, combat.OnHitBuffLastSummary.RequestedBuffCount);
            Assert.That(combat.DamageTelemetryRows, Has.Some.Matches<DamageTelemetryRow>(row =>
                row.SourceKind == DamageProfileSourceKind.ProjectileImpact
                && row.ActionId == CharacterTestCharacterActionConfigSource.ChannelBeamActionId
                && row.StableEventId == CharacterTestCharacterActionConfigSource.ChannelBeamProjectileImpactStableEventId
                && (row.DamageProfileId == 0)));
            Assert.That(combat.HitPresentationEvents, Has.Some.Matches<HitPresentationEvent>(evt =>
                evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.ChannelBeamPresentationProfileId));
        }

[Test]
        public void CharacterActionRuntime_HookChainProjectile_HitCreatesPullAndMovesTargetTowardCaster()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 7f, 180f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.HookChainIntentId,
                RuntimeFrame.Zero), "Hook Chain intent should start. reject=" + attacker.Action.LastRejectReason);

            var results = new List<HitResolveResult>();
            bool sawPullState = false;
            bool sawPullRequest = false;
            bool acceptedHit = false;
            bool sawTelemetry = false;
            bool placedTargetOnProjectileLine = false;
            int initialTargetZ = target.Transform.ZMillimeters;
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            for (int i = 0; i < 90; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
                if (!placedTargetOnProjectileLine)
                {
                    CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                    if (projectileState != null && projectileState.ActiveCount > 0)
                    {
                        PlaceTargetOnProjectileLine(
                            physicsRuntime,
                            target,
                            projectileState.ActiveProjectiles[0],
                            Fix64.FromInt(6));
                        initialTargetZ = target.Transform.ZMillimeters;
                        placedTargetOnProjectileLine = true;
                    }
                }

                if (attacker.LastCombatHitResults.Count > 0)
                    results.AddRange(attacker.LastCombatHitResults);
                acceptedHit |= attacker.LastCombatHitResults.Any(result =>
                    result.IsAcceptedDamage
                    && result.TargetId.Equals(physicsRuntime.GetEntityId(target)));
                sawTelemetry |= combat.DamageTelemetryRows.Any(row =>
                    row.SourceKind == DamageProfileSourceKind.ProjectileImpact
                    && row.StableEventId == CharacterTestCharacterActionConfigSource.HookChainProjectileImpactStableEventId
                    && (row.DamageProfileId == 0));
                sawPullState |= combat.ActiveHookPullCount > 0;
                sawPullRequest |= target.MotionRequests.Any(request =>
                    request.Kind == CharacterTestCharacterMotionRequestKind.ExternalForce
                    && request.SourceActionId == CharacterTestCharacterActionConfigSource.HookChainActionId
                    && request.StableEventId == CharacterTestCharacterActionConfigSource.HookChainProjectileImpactStableEventId);
                if (sawPullState && sawPullRequest && target.Transform.ZMillimeters < initialTargetZ - 300)
                    break;
            }

            Assert.IsTrue(
                acceptedHit && sawTelemetry && sawPullState && sawPullRequest
                && target.Transform.ZMillimeters < initialTargetZ - 300,
                "Hook Chain pull diagnostics: placedTargetOnProjectileLine=" + placedTargetOnProjectileLine
                + " acceptedHit=" + acceptedHit
                + " telemetry=" + sawTelemetry
                + " sawPullState=" + sawPullState
                + " sawPullRequest=" + sawPullRequest
                + " targetZ=" + target.Transform.ZMillimeters
                + " initialZ=" + initialTargetZ
                + " activePulls=" + combat.ActiveHookPullCount
                + " resultCount=" + results.Count);
            Assert.IsTrue(placedTargetOnProjectileLine, "Hook Chain projectile should spawn before validating tether pull.");
            Assert.IsTrue(sawPullState, "Hook Chain impact should register an active tether pull.");
            Assert.IsTrue(sawPullRequest, "Hook Chain pull should enqueue deterministic external-force motion requests.");
            Assert.Less(target.Transform.ZMillimeters, initialTargetZ - 300);
            CharacterTestHookPullRuntimeState hookPullState = combat.CreateHookPullStateSnapshot();
            Assert.Greater(hookPullState.ActiveCount, 0);
            CharacterTestNetworkHookPullRuntimeSnapshot networkSnapshot =
                CharacterTestNetworkHookPullSnapshotCodec.Capture(combat);
            Assert.AreEqual(hookPullState.ActiveCount, networkSnapshot.ActiveCount);
            var restoredCombat = new CharacterTestCharacterCombatTraceSystem();
            restoredCombat.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
            NetworkSnapshotApplyResult applyResult =
                CharacterTestNetworkHookPullSnapshotCodec.Apply(restoredCombat, networkSnapshot);
            Assert.IsTrue(applyResult.Success, applyResult.Message);
            RuntimeHashAccumulator originalHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkHookPullSnapshotCodec.AddStateHash(originalHash, hookPullState);
            RuntimeHashAccumulator restoredHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkHookPullSnapshotCodec.AddStateHash(
                restoredHash,
                restoredCombat.CreateHookPullStateSnapshot());
            Assert.AreEqual(originalHash.ToHash(), restoredHash.ToHash());
            RuntimeHashAccumulator ownedHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkHookPullSnapshotCodec.AddStateHash(
                ownedHash,
                hookPullState,
                hookPullState.ActivePulls[0].CasterEntityId);
            Assert.AreEqual(originalHash.ToHash(), ownedHash.ToHash());
            RuntimeHashAccumulator nonOwnedHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkHookPullSnapshotCodec.AddStateHashExcludingCaster(
                nonOwnedHash,
                hookPullState,
                hookPullState.ActivePulls[0].CasterEntityId);
            Assert.AreNotEqual(originalHash.ToHash(), nonOwnedHash.ToHash());
            CharacterTestNetworkHookPullRuntimeSnapshot changedNetworkSnapshot =
                networkSnapshot.CloneForRollback();
            changedNetworkSnapshot.ActivePulls[0].StableEventId += 1;
            RuntimeHashAccumulator changedFieldHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkHookPullSnapshotCodec.AddStateHash(
                changedFieldHash,
                changedNetworkSnapshot.ToState());
            Assert.AreNotEqual(originalHash.ToHash(), changedFieldHash.ToHash());
            AssertNoStasisBuff(world, target);
        }

[Test]
        public void CharacterActionRuntime_ArcMine_WithoutLockedTargetThrowsForwardCarrierThenDeploysVfx()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(8f, 0f, 8f, 180f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            combat.SetVfxRuntime(vfx);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.ArcMineIntentId,
                RuntimeFrame.Zero), "Arc Mine intent should start without a locked target. reject=" + attacker.Action.LastRejectReason);

            bool sawCarrierProjectile = false;
            bool capturedMine = false;
            bool sawDeployVfx = false;
            bool sawWaitingVfx = false;
            bool sawWaitingRefreshVfx = false;
            bool capturedExpectedFallbackTarget = false;
            CombatProjectileSnapshot carrierProjectile = default;
            CharacterTestArcMineSnapshot deployedMine = default;
            FixVector3 expectedFallbackTarget = FixVector3.Zero;
            FixVector3 expectedFallbackForward = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
            var pendingVfx = new List<VfxSpawnRequest>();
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            for (int i = 0; i < 140; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);

                if (!sawCarrierProjectile)
                {
                    CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                    if (projectileState != null && projectileState.ActiveCount > 0)
                    {
                        carrierProjectile = projectileState.ActiveProjectiles[0];
                        if (carrierProjectile.DefinitionId == CharacterTestFireballProjectileAdapter.ArcMineDefinitionId)
                        {
                            sawCarrierProjectile = true;
                            expectedFallbackTarget = ResolveExpectedArcMineForwardTarget(
                                attacker,
                                physicsRuntime,
                                out expectedFallbackForward);
                            capturedExpectedFallbackTarget = true;
                        }
                    }
                }

                if (!capturedMine && combat.ActiveArcMineCount > 0)
                {
                    CharacterTestArcMineRuntimeState state = combat.CreateArcMineStateSnapshot();
                    Assert.Greater(state.ActiveCount, 0);
                    deployedMine = state.ActiveMines[0];
                    capturedMine = true;
                }

                pendingVfx.Clear();
                vfx.CopyPendingSpawnRequests(pendingVfx);
                for (int vfxIndex = 0; vfxIndex < pendingVfx.Count; vfxIndex++)
                {
                    VfxSpawnRequest request = pendingVfx[vfxIndex];
                    if (request.DefinitionId.Equals(new VfxDefinitionId(CharacterTestVfxIds.ArcMineDeployDefinitionId)))
                    {
                        Assert.AreEqual(CharacterTestVfxIds.ArcMineDeployLifetimeFrames, request.LifetimeFramesOverride);
                        sawDeployVfx = true;
                    }
                    else if (request.DefinitionId.Equals(new VfxDefinitionId(CharacterTestVfxIds.ArcMineFuseDefinitionId))
                        && (request.TraceId.KindId == CharacterTestFrameworkTraceKinds.CombatRuntimeEffectVfx
                            && request.TraceId.Sequence == CharacterTestCharacterActionConfigSource.ArcMineWaitingVfxStableEventId))
                    {
                        Assert.AreEqual(CharacterTestVfxIds.ArcMineFuseLifetimeFrames, request.LifetimeFramesOverride);
                        if (capturedMine
                            && request.Frame.Value >= deployedMine.CreatedFrame
                                + CharacterTestArcMineIds.WaitingVfxRefreshIntervalFrames)
                        {
                            sawWaitingRefreshVfx = true;
                        }
                        else
                        {
                            sawWaitingVfx = true;
                        }
                    }
                }

                if (capturedMine && sawDeployVfx && sawWaitingVfx && sawWaitingRefreshVfx)
                    break;
            }

            Assert.IsTrue(sawCarrierProjectile, "Arc Mine should throw a carrier projectile without target lock.");
            Assert.IsTrue(capturedExpectedFallbackTarget);
            Assert.IsTrue(carrierProjectile.HasTargetPoint);
            long targetToleranceRaw = Fix64.FromRatio(1, 1000).RawValue;
            AssertFixVectorNear(expectedFallbackTarget, carrierProjectile.TargetPoint, targetToleranceRaw);
            Assert.IsTrue(capturedMine, "Arc Mine should deploy after the carrier projectile lands.");
            AssertFix64Near(carrierProjectile.TargetPoint.X, deployedMine.Center.X, targetToleranceRaw);
            Assert.AreEqual(Fix64.Zero.RawValue, deployedMine.Center.Y.RawValue);
            AssertFix64Near(carrierProjectile.TargetPoint.Z, deployedMine.Center.Z, targetToleranceRaw);
            AssertFixVectorNear(expectedFallbackForward, deployedMine.Forward, targetToleranceRaw);
            Assert.IsTrue(sawDeployVfx, "Arc Mine deploy should enqueue a visible deploy VFX request.");
            Assert.IsTrue(sawWaitingVfx, "Arc Mine deployment should enqueue a persistent waiting VFX request.");
            Assert.IsTrue(sawWaitingRefreshVfx, "Arc Mine should refresh waiting VFX while it remains armed and untriggered.");
        }

[Test]
        public void CharacterActionRuntime_ArcMine_CarrierDropDeploysMineThenFuseBlastsHostile()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 5f, 180f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            combat.SetVfxRuntime(vfx);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.ArcMineIntentId,
                RuntimeFrame.Zero), "Arc Mine intent should start. reject=" + attacker.Action.LastRejectReason);

            bool sawCarrierProjectile = false;
            bool sawDropProjectile = false;
            bool sawActiveMine = false;
            bool capturedFuseSnapshot = false;
            bool triggered = false;
            bool acceptedHit = false;
            bool sawTelemetry = false;
            bool sawPresentation = false;
            bool sawFuseVfx = false;
            bool sawTriggerVfx = false;
            bool damagedBeforeTrap = false;
            bool damagedBeforeFuseDetonate = false;
            long expectedDetonateFrame = -1L;
            int initialHp = world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp);
            var pendingVfx = new List<VfxSpawnRequest>();
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            for (int i = 0; i < 140; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);

                pendingVfx.Clear();
                vfx.CopyPendingSpawnRequests(pendingVfx);
                for (int vfxIndex = 0; vfxIndex < pendingVfx.Count; vfxIndex++)
                {
                    VfxSpawnRequest request = pendingVfx[vfxIndex];
                    if (request.DefinitionId.Equals(new VfxDefinitionId(CharacterTestVfxIds.ArcMineFuseDefinitionId))
                        && (request.TraceId.KindId == CharacterTestFrameworkTraceKinds.CombatRuntimeEffectVfx
                            && request.TraceId.Sequence == CharacterTestCharacterActionConfigSource.ArcMineFuseVfxStableEventId))
                    {
                        Assert.AreEqual(CharacterTestVfxIds.ArcMineFuseLifetimeFrames, request.LifetimeFramesOverride);
                        sawFuseVfx = true;
                    }
                    else if (request.DefinitionId.Equals(new VfxDefinitionId(CharacterTestVfxIds.ArcMineTriggerDefinitionId)))
                    {
                        Assert.AreEqual(CharacterTestVfxIds.ArcMineTriggerLifetimeFrames, request.LifetimeFramesOverride);
                        sawTriggerVfx = true;
                    }
                }

                if (!sawCarrierProjectile)
                {
                    CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                    if (projectileState != null && projectileState.ActiveCount > 0)
                    {
                        for (int projectileIndex = 0; projectileIndex < projectileState.ActiveCount; projectileIndex++)
                        {
                            CombatProjectileSnapshot projectile = projectileState.ActiveProjectiles[projectileIndex];
                            if (projectile.DefinitionId == CharacterTestFireballProjectileAdapter.ArcMineDefinitionId)
                            {
                                sawCarrierProjectile = true;
                                Assert.IsTrue(projectile.HasTargetPoint);
                                AssertFixVector(0f, 0f, 5f, projectile.TargetPoint);
                            }
                        }
                    }
                }

                if (!sawDropProjectile)
                {
                    CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                    for (int projectileIndex = 0; projectileState != null && projectileIndex < projectileState.ActiveCount; projectileIndex++)
                    {
                        if (projectileState.ActiveProjectiles[projectileIndex].DefinitionId
                            == CharacterTestFireballProjectileAdapter.ArcMineDropDefinitionId)
                        {
                            sawDropProjectile = true;
                            break;
                        }
                    }
                }

                if (!sawActiveMine
                    && combat.ActiveArcMineCount == 0
                    && world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp) < initialHp)
                {
                    damagedBeforeTrap = true;
                }

                sawActiveMine |= combat.ActiveArcMineCount > 0;
                if (combat.ActiveArcMineCount > 0)
                {
                    CharacterTestArcMineRuntimeState arcMineState = combat.CreateArcMineStateSnapshot();
                    Assert.Greater(arcMineState.ActiveCount, 0);
                    CharacterTestArcMineSnapshot mine = arcMineState.ActiveMines[0];
                    if (mine.DetonateFrame > 0)
                    {
                        Assert.AreEqual(
                            CharacterTestArcMineIds.FuseDelayFrames,
                            mine.DetonateFrame - mine.FuseStartedFrame);
                        expectedDetonateFrame = mine.DetonateFrame;
                        if (!capturedFuseSnapshot)
                        {
                            CharacterTestNetworkArcMineRuntimeSnapshot networkSnapshot =
                                CharacterTestNetworkArcMineSnapshotCodec.Capture(combat);
                            Assert.AreEqual(arcMineState.ActiveCount, networkSnapshot.ActiveCount);
                            Assert.AreEqual(mine.FuseStartedFrame, networkSnapshot.ActiveMines[0].FuseStartedFrame);
                            Assert.AreEqual(mine.DetonateFrame, networkSnapshot.ActiveMines[0].DetonateFrame);
                            Assert.AreEqual(mine.TriggerTargetEntityId, networkSnapshot.ActiveMines[0].TriggerTargetEntityId);
                            var restoredCombat = new CharacterTestCharacterCombatTraceSystem();
                            restoredCombat.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
                            NetworkSnapshotApplyResult applyResult =
                                CharacterTestNetworkArcMineSnapshotCodec.Apply(restoredCombat, networkSnapshot);
                            Assert.IsTrue(applyResult.Success, applyResult.Message);
                            RuntimeHashAccumulator originalHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
                            CharacterTestNetworkArcMineSnapshotCodec.AddStateHash(originalHash, arcMineState);
                            RuntimeHashAccumulator restoredHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
                            CharacterTestNetworkArcMineSnapshotCodec.AddStateHash(
                                restoredHash,
                                restoredCombat.CreateArcMineStateSnapshot());
                            Assert.AreEqual(originalHash.ToHash(), restoredHash.ToHash());
                            RuntimeHashAccumulator ownedHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
                            CharacterTestNetworkArcMineSnapshotCodec.AddStateHash(
                                ownedHash,
                                arcMineState,
                                mine.CasterEntityId);
                            Assert.AreEqual(originalHash.ToHash(), ownedHash.ToHash());
                            RuntimeHashAccumulator nonOwnedHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
                            CharacterTestNetworkArcMineSnapshotCodec.AddStateHashExcludingCaster(
                                nonOwnedHash,
                                arcMineState,
                                mine.CasterEntityId);
                            Assert.AreNotEqual(originalHash.ToHash(), nonOwnedHash.ToHash());
                            CharacterTestNetworkArcMineRuntimeSnapshot changedNetworkSnapshot =
                                networkSnapshot.CloneForRollback();
                            changedNetworkSnapshot.ActiveMines[0].TraceId += 1;
                            RuntimeHashAccumulator changedFieldHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
                            CharacterTestNetworkArcMineSnapshotCodec.AddStateHash(
                                changedFieldHash,
                                changedNetworkSnapshot.ToState());
                            Assert.AreNotEqual(originalHash.ToHash(), changedFieldHash.ToHash());
                            capturedFuseSnapshot = true;
                        }
                    }
                }

                if (expectedDetonateFrame > 0
                    && frame.Value < expectedDetonateFrame
                    && world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp) < initialHp)
                {
                    damagedBeforeFuseDetonate = true;
                }

                triggered |= combat.ArcMineLastTickSummary.TriggeredCount > 0;
                acceptedHit |= attacker.LastCombatHitResults.Any(result =>
                    result.IsAcceptedDamage
                    && result.TargetId.Equals(physicsRuntime.GetEntityId(target)));
                sawTelemetry |= combat.DamageTelemetryRows.Any(row =>
                    row.SourceKind == DamageProfileSourceKind.AreaPulse
                    && row.ActionId == CharacterTestCharacterActionConfigSource.ArcMineActionId
                    && row.StableEventId == CharacterTestCharacterActionConfigSource.ArcMineTriggerStableEventId
                    && row.AnalysisCategory == CharacterTestAuthoredContentIds.D411_Id16);
                sawPresentation |= combat.HitPresentationEvents.Any(evt =>
                    evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.ArcMinePresentationProfileId);
                if (triggered && acceptedHit && sawTelemetry && sawPresentation && sawFuseVfx && sawTriggerVfx)
                    break;
            }

            Assert.IsTrue(sawCarrierProjectile, "Arc Mine should throw a carrier projectile before creating a trap.");
            Assert.IsTrue(sawDropProjectile, "Arc Mine carrier should spawn a secondary drop projectile after hitting a hostile.");
            Assert.IsFalse(damagedBeforeTrap, "Arc Mine carrier flight must not damage hostile targets before trap deployment.");
            Assert.IsFalse(damagedBeforeFuseDetonate, "Arc Mine should not damage hostile targets before the fuse detonation frame.");
            Assert.IsTrue(sawActiveMine, "Arc Mine should create an active trap after the carrier projectile lands.");
            Assert.IsTrue(capturedFuseSnapshot, "Arc Mine fuse state should be snapshot/hash covered before detonation.");
            Assert.IsTrue(triggered, "Arc Mine should detonate after the fuse delay when a hostile has entered trigger radius.");
            Assert.IsTrue(acceptedHit, "Arc Mine blast should resolve accepted hostile damage through HitResolve.");
            Assert.IsTrue(sawTelemetry, "Arc Mine blast should emit AreaPulse damage telemetry.");
            Assert.IsTrue(sawPresentation, "Arc Mine blast should emit hit presentation using its profile.");
            Assert.IsTrue(sawFuseVfx, "Arc Mine should enqueue fuse VFX when a hostile starts the fuse.");
            Assert.IsTrue(sawTriggerVfx, "Arc Mine should enqueue trigger VFX when the fuse detonates.");
            Assert.Less(
                world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp),
                initialHp);
            Assert.AreEqual(0, combat.ActiveArcMineCount);
        }

[Test]
        public void CharacterActionRuntime_FireballLobProjectile_LockedTargetArcUsesFixedSpeedLifetime()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(4f, 0f, 8f, 0f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.FireballLobIntentId,
                RuntimeFrame.Zero));

            CombatProjectileSnapshot projectile = RunUntilProjectileSpawn(world, runtime, combat, maxFrames: 10);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballLobDefinitionId, projectile.DefinitionId);
            Assert.IsTrue(projectile.HasTargetPoint);
            AssertFixVector(4f, 1f, 8f, projectile.TargetPoint);
            Assert.IsTrue((projectile.TargetPoint - projectile.Origin).TryNormalize(out FixVector3 expectedDirection));
            AssertFixVectorNear(expectedDirection, projectile.Direction, toleranceRaw: 16L);
            Assert.AreEqual(
                ExpectedProjectileLifetimeFrames(
                    projectile.Origin,
                    projectile.TargetPoint,
                    Fix64.FromRatio(35, 100)),
                projectile.EffectiveLifetimeFrames);
        }

[Test]
        public void CharacterActionRuntime_PoisonBottleProjectile_LockedTargetUsesSolvedLaunchSpeed()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 7f, 0f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.PoisonBottleIntentId,
                RuntimeFrame.Zero));

            CombatProjectileSnapshot projectile = RunUntilProjectileSpawn(world, runtime, combat, maxFrames: 40);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.PoisonBottleDefinitionId, projectile.DefinitionId);
            Assert.IsTrue(projectile.HasTargetPoint);
            AssertFixVector(0f, 1f, 7f, projectile.TargetPoint);
            Assert.IsTrue(CombatProjectileBallistics.TrySolveLaunchArc(
                projectile.Origin,
                projectile.TargetPoint,
                CharacterTestFireballProjectileAdapter.PoisonBottleSpeedPerFrame,
                CharacterTestFireballProjectileAdapter.PoisonBottleGravityPerFrameSquared,
                Fix64.Zero,
                CharacterTestFireballProjectileAdapter.PoisonBottleLifetimeFrames,
                out FixVector3 expectedInitialVelocity,
                out Fix64 expectedLaunchSpeedPerFrame,
                out int expectedLifetimeFrames));
            Assert.IsTrue(expectedInitialVelocity.TryNormalize(out FixVector3 expectedDirection));
            AssertFixVectorNear(expectedDirection, projectile.Direction, toleranceRaw: 16L);
            AssertFix64Near(expectedLaunchSpeedPerFrame, projectile.LaunchSpeedPerFrame, toleranceRaw: 16L);
            Assert.Less(projectile.LaunchSpeedPerFrame.RawValue, CharacterTestFireballProjectileAdapter.PoisonBottleSpeedPerFrame.RawValue);
            Assert.AreEqual(expectedLifetimeFrames, projectile.EffectiveLifetimeFrames);
        }

[Test]
        public void CharacterActionRuntime_MeteorBarrageProjectile_UsesFixedLandingContextAndSkyOrigin()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(4f, 0f, 8f, 0f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            combat.SetVfxRuntime(vfx);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.MeteorBarrageIntentId,
                RuntimeFrame.Zero));

            runtime.TickEntity(attacker);
            combat.Tick(world, runtime, RuntimeFrame.Zero);

            Assert.AreEqual(1, combat.ActiveMeteorBarrageCastContextCount);
            var telegraphs = new List<VfxSpawnRequest>();
            Assert.AreEqual(
                CharacterTestFireballProjectileAdapter.MeteorBarrageProjectileCount,
                vfx.CopyPendingSpawnRequests(telegraphs));
            Assert.AreEqual(
                new VfxDefinitionId(CharacterTestVfxIds.MeteorBarrageTelegraphDefinitionId),
                telegraphs[0].DefinitionId);
            Assert.AreEqual(VfxAnchorKind.World, telegraphs[0].Anchor.Kind);
            AssertVfxVector(4f, 0f, 8f, telegraphs[0].Anchor.Pose.Position);
            for (int i = 0; i < telegraphs.Count; i++)
            {
                Assert.AreEqual(6 + i * 6, telegraphs[i].Frame.Value);
                Assert.AreEqual(6 + i * 6, telegraphs[i].LocalFrame);
                Assert.AreEqual(i, telegraphs[i].Parameters.Value2);
                Assert.Greater(telegraphs[i].Parameters.Value3, 0);
                Assert.Greater(
                    telegraphs[i].LifetimeFramesOverride,
                    CharacterTestVfxIds.MeteorBarrageTelegraphLifetimeFrames);
            }

            target.SetTransform(new CharacterTestCharacterTransformComponent(8f, 0f, 4f, 0f));
            CombatProjectileSnapshot projectile = RunUntilProjectileSpawn(
                world,
                runtime,
                combat,
                maxFrames: 40,
                startFrame: 1);

            Assert.AreEqual(CharacterTestFireballProjectileAdapter.MeteorBarrageDefinitionId, projectile.DefinitionId);
            Assert.IsTrue(projectile.HasTargetPoint);
            AssertFixVector(4f, 0f, 8f, projectile.TargetPoint);
            AssertFixVector(4f, 8f, 6f, projectile.Origin);
            Assert.IsTrue((projectile.TargetPoint - projectile.Origin).TryNormalize(out FixVector3 expectedDirection));
            AssertFixVectorNear(expectedDirection, projectile.Direction, toleranceRaw: 16L);
            Assert.AreEqual(
                ExpectedProjectileLifetimeFrames(
                    projectile.Origin,
                    projectile.TargetPoint,
                    CharacterTestFireballProjectileAdapter.MeteorBarrageSpeedPerFrame),
                projectile.EffectiveLifetimeFrames);

            Assert.IsTrue(combat.TryResolveMeteorBarrageLandingPoint(
                attacker.Id.Value,
                projectile.ActionInstanceId,
                5,
                out FixVector3 finalLanding));
            AssertFixVector(5.4f, 0f, 7.7f, finalLanding);
        }

[Test]
        public void CharacterActionRuntime_MeteorBarrageGroundImpact_SpawnsImpactVfxAndStopsTelegraph()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => 0);
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            combat.SetVfxRuntime(vfx);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.MeteorBarrageIntentId,
                RuntimeFrame.Zero));

            var events = new List<VfxRuntimeEvent>();
            int telegraphSpawnCount = 0;
            int telegraphStopCount = 0;
            int impactSpawnCount = 0;
            RuntimeFrame impactFrame = RuntimeFrame.Zero;
            VfxVector3 impactPosition = default;
            for (int i = 0; i < 90; i++)
            {
                var frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                combat.Tick(world, runtime, frame);
                vfx.Tick(new RuntimeTickContext(i, 0d, 0d, RuntimeTickStage.PostSimulation));
                events.Clear();
                vfx.DrainEvents(frame, events);
                for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                {
                    VfxRuntimeEvent runtimeEvent = events[eventIndex];
                    if (runtimeEvent.DefinitionId.Value == CharacterTestVfxIds.MeteorBarrageTelegraphDefinitionId)
                    {
                        if (runtimeEvent.Type == VfxRuntimeEventType.Spawned)
                            telegraphSpawnCount++;
                        else if (runtimeEvent.Type == VfxRuntimeEventType.Stopped)
                            telegraphStopCount++;
                    }
                    else if (runtimeEvent.Type == VfxRuntimeEventType.Spawned
                        && runtimeEvent.DefinitionId.Value == CharacterTestVfxIds.MeteorBarrageImpactDefinitionId)
                    {
                        impactSpawnCount++;
                        impactFrame = frame;
                        impactPosition = runtimeEvent.Anchor.Pose.Position;
                    }
                }

                if (impactSpawnCount > 0 && telegraphStopCount > 0)
                    break;
            }

            Assert.GreaterOrEqual(telegraphSpawnCount, 1);
            Assert.AreEqual(1, impactSpawnCount);
            Assert.AreEqual(1, telegraphStopCount);
            Assert.GreaterOrEqual(impactFrame.Value, 24);
            AssertVfxVectorNear(0f, 0f, 18f, impactPosition, tolerance: 0.2f);
            Assert.AreEqual(0, attacker.LastCombatHitResults.Count);
        }

[Test]
        public void CharacterActionRuntime_MeteorBarrageGroundImpact_RecordsRadialInteractionWithoutAreaTargets()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => 0);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.MeteorBarrageIntentId,
                RuntimeFrame.Zero));

            bool sawInteraction = false;
            CharacterTestInteractionEvent interaction = default;
            for (int i = 0; i < 90; i++)
            {
                var frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                combat.Tick(world, runtime, frame);
                if (combat.LastFrameInteractionEvents.Count == 0)
                    continue;

                Assert.AreEqual(1, combat.LastFrameInteractionEvents.Count);
                interaction = combat.LastFrameInteractionEvents[0];
                sawInteraction = true;
                break;
            }

            Assert.IsTrue(sawInteraction, "Expected the first meteor ground impact to record an interaction.");
            Assert.AreEqual(1, combat.InteractionHistory.Count);
            Assert.AreEqual(CharacterTestInteractionKind.PhysicalField, interaction.Kind);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileImpactStableEventId,
                interaction.StableId);
            Assert.AreEqual(CharacterTestInteractionSourceDomains.CombatProjectileAreaPulse, interaction.SourceDomain);
            Assert.AreEqual(attacker.Id.Value, interaction.SourceEntityId);
            Assert.AreEqual(0, interaction.TargetEntityId);
            AssertFixVectorNear(
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.FromInt(18)),
                interaction.AnchorPosition,
                Fix64.FromRatio(1, 5).RawValue);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.MeteorBarrageAreaRadius, interaction.Radius);
            Assert.AreEqual(12, interaction.Intensity);
            Assert.AreEqual(CharacterTestInteractionFieldShape.Sphere, interaction.PhysicalField.Shape);
            Assert.AreEqual(CharacterTestInteractionForceMode.RadialImpulse, interaction.PhysicalField.ForceMode);
            Assert.IsTrue(interaction.PhysicalField.Targets(CharacterTestInteractionTargetFilter.DynamicCharacters));
            Assert.IsTrue(interaction.PhysicalField.Targets(CharacterTestInteractionTargetFilter.DynamicMovableObjects));
            Assert.IsTrue(interaction.PhysicalField.Targets(CharacterTestInteractionTargetFilter.ExcludeSource));
            Assert.AreEqual(0, attacker.LastCombatHitResults.Count);
        }

[Test]
        public void CharacterActionRuntime_MeteorBarrageTargetImpact_DamagesMultipleTargetsAndRecordsSingleInteraction()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            firstTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 18f, 0f));
            secondTarget.SetTransform(new CharacterTestCharacterTransformComponent(0.75f, 0f, 18f, 0f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => firstTarget.Id.Value);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.MeteorBarrageIntentId,
                RuntimeFrame.Zero));

            int acceptedDamageCount = 0;
            for (int i = 0; i < 90; i++)
            {
                var frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
                acceptedDamageCount += attacker.LastCombatHitResults.Count(result => result.IsAcceptedDamage);
                if (combat.LastFrameInteractionEvents.Count > 0)
                    break;
            }

            Assert.AreEqual(2, acceptedDamageCount);
            Assert.AreEqual(1, combat.InteractionHistory.Count);
            CharacterTestInteractionEvent interaction = combat.InteractionHistory.Events.Single();
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileImpactStableEventId,
                interaction.StableId);
            Assert.AreEqual(CharacterTestInteractionSourceDomains.CombatProjectileAreaPulse, interaction.SourceDomain);
            Assert.AreEqual(12, interaction.Intensity);
            Assert.AreEqual(CharacterTestInteractionForceMode.RadialImpulse, interaction.PhysicalField.ForceMode);
            Assert.AreEqual(
                CharacterTestCharacterCombatStateComponent.DefaultMaxHealth - 8,
                world.GetCurrentAttribute(firstTarget, CharacterTestCharacterAttributeIds.Hp));
            Assert.AreEqual(
                CharacterTestCharacterCombatStateComponent.DefaultMaxHealth - 8,
                world.GetCurrentAttribute(secondTarget, CharacterTestCharacterAttributeIds.Hp));
        }

[Test]
        public void CharacterActionRuntime_MeteorBarrageGroundImpact_PushesMovableNaturalObjects()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                    "movable.meteor.target",
                    601,
                    "movable-natural-object",
                    massKg: 80f,
                    CombatSurfaceMaterial.Default,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(1f, 0.25f, 18f))
            });
            CharacterTestMovableNaturalObjectState movable = world.MovableNaturalObjects.Single();
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => 0);
            var interactionPhysics = new CharacterTestInteractionPhysicsSystem();

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.MeteorBarrageIntentId,
                RuntimeFrame.Zero));

            bool appliedMovableImpulse = false;
            for (int i = 0; i < 90; i++)
            {
                var frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
                interactionPhysics.Tick(world, physicsRuntime, combat.LastFrameInteractionEvents, frame);
                if (interactionPhysics.LastSummary.MovableTargetCount <= 0)
                    continue;

                appliedMovableImpulse = true;
                break;
            }

            Assert.IsTrue(appliedMovableImpulse, "Expected meteor radial interaction to affect a movable object.");
            Assert.AreEqual(1, combat.InteractionHistory.Count);
            Assert.AreEqual(1, interactionPhysics.LastSummary.MovableTargetCount);
            Assert.AreEqual(1, interactionPhysics.LastSummary.AppliedForceCount);
            Assert.AreEqual(601, interactionPhysics.LastSummary.LastMovableNumericId);
            Assert.AreEqual(1000, movable.XMillimeters);

            CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
            CharacterTestMovableNaturalObjectSystem.Tick(
                world,
                physicsRuntime,
                CharacterTestDeterministicUnits.DefaultTickRate,
                new RuntimeFrame(91));

            Assert.Greater(movable.VelocityXMillimetersPerSecond, 0);
            Assert.AreEqual(0, movable.VelocityZMillimetersPerSecond);
            Assert.Greater(movable.XMillimeters, 1000);
            Assert.Greater(movable.ContactPressure, 0);
            Assert.Greater(movable.ContactCount, 0);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileImpactStableEventId,
                movable.ContactAId);
        }

[Test]
        public void CharacterActionRuntime_PoisonBottleProjectile_NoLockedTargetUsesRangeOptimalForwardTarget()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => 0);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.PoisonBottleIntentId,
                RuntimeFrame.Zero));

            CombatProjectileSnapshot projectile = RunUntilProjectileSpawn(world, runtime, combat, maxFrames: 40);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.PoisonBottleDefinitionId, projectile.DefinitionId);
            Assert.IsTrue(projectile.HasTargetPoint);
            AssertFixVectorNear(
                projectile.Origin + new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One) * CharacterTestFireballProjectileAdapter.PoisonBottleRange,
                projectile.TargetPoint,
                toleranceRaw: 4L);
            Assert.IsTrue(CombatProjectileBallistics.TrySolveLaunchArc(
                projectile.Origin,
                projectile.TargetPoint,
                CharacterTestFireballProjectileAdapter.PoisonBottleSpeedPerFrame,
                CharacterTestFireballProjectileAdapter.PoisonBottleGravityPerFrameSquared,
                Fix64.Zero,
                CharacterTestFireballProjectileAdapter.PoisonBottleLifetimeFrames,
                out FixVector3 expectedInitialVelocity,
                out Fix64 expectedLaunchSpeedPerFrame,
                out int expectedLifetimeFrames));
            Assert.IsTrue(expectedInitialVelocity.TryNormalize(out FixVector3 expectedDirection));
            AssertFixVectorNear(expectedDirection, projectile.Direction, toleranceRaw: 16L);
            AssertFix64Near(expectedLaunchSpeedPerFrame, projectile.LaunchSpeedPerFrame, toleranceRaw: 16L);
            Assert.AreEqual(expectedLifetimeFrames, projectile.EffectiveLifetimeFrames);
        }

[Test]
        public void CharacterActionRuntime_FireballHomingProjectile_TracksLockedTargetCenterAfterSpawn()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(4f, 0f, 8f, 0f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.FireballHomingIntentId,
                RuntimeFrame.Zero));

            CombatProjectileSnapshot projectile = RunUntilProjectileSpawn(world, runtime, combat, maxFrames: 10);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.FireballHomingDefinitionId, projectile.DefinitionId);
            Assert.IsTrue(projectile.HasTrackingTarget);
            Assert.AreEqual(physicsRuntime.GetEntityId(target), projectile.TrackingTargetEntityId);
            AssertFixVector(4f, 1f, 8f, projectile.TargetPoint);

            target.SetTransform(new CharacterTestCharacterTransformComponent(8f, 0f, 4f, 0f));
            RuntimeFrame nextFrame = new RuntimeFrame(projectile.SpawnFrame.Value + 1);
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
                runtime.TickEntity(entities[i]);
            combat.Tick(world, runtime, nextFrame);
            CombatProjectileSnapshot moved = combat.CreateProjectileStateSnapshot().ActiveProjectiles[0];

            Assert.IsTrue(moved.HasTrackingTarget);
            AssertFixVector(8f, 1f, 4f, moved.TargetPoint);
            Assert.IsTrue((moved.TargetPoint - projectile.Position).TryNormalize(out FixVector3 expectedDirection));
            Assert.Greater(
                moved.Direction.Dot(expectedDirection).RawValue,
                projectile.Direction.Dot(expectedDirection).RawValue,
                "Homing direction should steer toward the updated tracking target center.");
        }

[Test]
        public void CharacterActionRuntime_FireballWindupVfx_EnqueuesActionPresentationSpawnAtCasterPose()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            attacker.SetTransform(new CharacterTestCharacterTransformComponent(2f, 3f, 4f, 90f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.FireballIntentId,
                RuntimeFrame.Zero));
            Assert.AreEqual(1, attacker.PendingVfxRequestCount);

            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            var bridge = new CharacterTestActionVfxBridgeModule(
                () => world.Entities,
                vfx,
                NullRuntimeLogger.Instance);
            bridge.Tick(new RuntimeTickContext(42, 0d, 0d, RuntimeTickStage.PostSimulation));

            Assert.AreEqual(1, bridge.LastFrameRequestCount);
            Assert.AreEqual(1, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(0, bridge.LastFrameFailureCount);
            Assert.AreEqual(0, attacker.PendingVfxRequestCount);

            var pending = new List<VfxSpawnRequest>();
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pending));
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.FireballChargeDefinitionId), pending[0].DefinitionId);
            Assert.AreEqual(VfxSourceDomains.ActionPresentation, pending[0].SourceDomain);
            Assert.AreEqual(42, pending[0].SourceFrame);
            Assert.AreEqual(0, pending[0].LocalFrame);
            Assert.AreEqual(attacker.Id.Value, pending[0].SourceId);
            Assert.AreEqual(6, pending[0].LifetimeFramesOverride);
            Assert.AreEqual(VfxAnchorKind.SourceEntity, pending[0].Anchor.Kind);
            Assert.AreEqual(attacker.Id.Value, pending[0].Anchor.SourceId);
            AssertVfxVector(2.65f, 4.15f, 4f, pending[0].Anchor.Pose.Position);
            AssertVfxVector(1f, 0f, 0f, pending[0].Anchor.Pose.Forward);
        }

        [Test]
        public void CharacterActionVfxBridge_MapsFormalVisualCueConfiguration()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            attacker.EnqueueVfxRequest(new CharacterActionVfxRequest(
                new CharacterActionAdapterDispatchMetadata(
                    RuntimeFrame.Zero,
                    instanceId: 1,
                    planId: 2,
                    actionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("action.formal"),
                    localFrame: 4,
                    stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0084,
                    sourceId: attacker.Id.Value,
                    traceId: new RuntimeTraceIdentity(942318)),
                "actor.test",
                CharacterTestVfxIds.FireballChargeResourceKey.ToString(),
                endFrame: 8,
                vfxConfig: new CharacterActionVfxEventConfig(
                    attachment: new MxAttachmentTransform(
                        MxAttachmentMode.FollowSocket,
                        "mainHand",
                        MxTransformSpace.Local,
                        new MxTransformOffset(
                            positionX: 0.25f,
                            positionY: 0.5f,
                            positionZ: 0.75f,
                            rotationEulerX: 10f,
                            rotationEulerY: 20f,
                            rotationEulerZ: 30f,
                            scaleX: 1.5f,
                            scaleY: 2f,
                            scaleZ: 2.5f)),
                    playbackMode: CharacterActionVfxPlaybackMode.StopAtEventEnd,
                    playbackRate: 0.5f,
                    scaleMultiplier: 1.25f,
                    startOffsetFrames: 2,
                    endOffsetFrames: 1,
                    stopPolicy: CharacterActionVfxStopPolicy.Dissipate,
                    parameter0: 7,
                    parameter1: 8,
                    parameter2: 9,
                    parameter3: 10)));

            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            var bridge = new CharacterTestActionVfxBridgeModule(
                () => world.Entities,
                vfx,
                NullRuntimeLogger.Instance);
            bridge.Tick(new RuntimeTickContext(42, 0d, 0d, RuntimeTickStage.PostSimulation));

            var pending = new List<VfxSpawnRequest>();
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pending));
            VfxSpawnRequest request = pending[0];
            Assert.AreEqual(5, request.LifetimeFramesOverride);
            Assert.AreEqual(VfxStopMode.Dissipate, request.ExpireStopMode);
            Assert.AreEqual(500, request.PlaybackRatePermille);
            Assert.AreEqual(2, request.SourceStartOffsetFrames);
            Assert.AreEqual(1, request.SourceEndOffsetFrames);
            Assert.AreEqual(VfxAnchorKind.Attached, request.Anchor.Kind);
            Assert.Greater(request.Anchor.SocketId, 0);
            AssertVfxVector(0.25f, 0.5f, 0.75f, request.Anchor.Pose.Position);
            AssertVfxVector(10f, 20f, 30f, request.Anchor.Pose.RotationEulerDegrees);
            AssertVfxVector(1.875f, 2.5f, 3.125f, request.Anchor.Pose.Scale);
            Assert.AreEqual(7, request.Parameters.Value0);
            Assert.AreEqual(8, request.Parameters.Value1);
            Assert.AreEqual(9, request.Parameters.Value2);
            Assert.AreEqual(10, request.Parameters.Value3);
        }

        [Test]
        public void CharacterActionRuntime_BeamLanceVfx_EnqueuesChargeThenBeamPresentationSpawns()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.BeamLanceActionId,
                out CharacterActionConfig action));
            Assert.AreEqual(2, action.PresentationTrack.Events.Length);
            PresentationTrackEvent chargeEvent = action.PresentationTrack.Events[0];
            PresentationTrackEvent beamEvent = action.PresentationTrack.Events[1];
            Assert.AreEqual(0, chargeEvent.Frame);
            Assert.AreEqual(29, chargeEvent.EndFrame);
            Assert.AreEqual(CharacterActionTrackEventKind.SpawnVisualCue, chargeEvent.Kind);
            Assert.AreEqual(CharacterTestVfxIds.BeamLanceChargeResourceKey.ToString(), chargeEvent.ResourceKey);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.BeamLanceChargeVfxStableEventId,
                chargeEvent.StableEventId);
            Assert.AreEqual(30, beamEvent.Frame);
            Assert.AreEqual(60, beamEvent.EndFrame);
            Assert.AreEqual(CharacterActionTrackEventKind.SpawnVisualCue, beamEvent.Kind);
            Assert.AreEqual(CharacterTestVfxIds.BeamLanceBeamResourceKey.ToString(), beamEvent.ResourceKey);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.BeamLanceBeamVfxStableEventId,
                beamEvent.StableEventId);
            Assert.AreEqual(1, action.GameplayTrack.Events.Length);
            Assert.AreEqual(30, action.GameplayTrack.Events[0].Frame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.BeamLanceProjectileRequestId,
                action.GameplayTrack.Events[0].RequestId);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.BeamLanceIntentId,
                RuntimeFrame.Zero));
            Assert.AreEqual(1, attacker.PendingVfxRequestCount);

            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            var bridge = new CharacterTestActionVfxBridgeModule(
                () => world.Entities,
                vfx,
                NullRuntimeLogger.Instance);
            bridge.Tick(new RuntimeTickContext(42, 0d, 0d, RuntimeTickStage.PostSimulation));

            Assert.AreEqual(1, bridge.LastFrameRequestCount);
            Assert.AreEqual(1, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(0, bridge.LastFrameFailureCount);
            var pending = new List<VfxSpawnRequest>();
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pending));
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.BeamLanceChargeDefinitionId), pending[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.BeamLanceChargeLifetimeFrames, pending[0].LifetimeFramesOverride);
            Assert.AreEqual(VfxSourceDomains.ActionPresentation, pending[0].SourceDomain);
            Assert.AreEqual(attacker.Id.Value, pending[0].SourceId);
            Assert.AreEqual(0, pending[0].LocalFrame);

            for (int i = 0; i < 30; i++)
                runtime.TickEntity(attacker);

            Assert.AreEqual(1, attacker.PendingVfxRequestCount);
            Assert.AreEqual(1, attacker.PendingGameplayRequestCount);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.BeamLanceProjectileRequestId,
                attacker.GameplayRequests[0].RequestId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.BeamLanceProjectileImpactStableEventId,
                attacker.GameplayRequests[0].Metadata.StableEventId);

            vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            bridge = new CharacterTestActionVfxBridgeModule(
                () => world.Entities,
                vfx,
                NullRuntimeLogger.Instance);
            bridge.Tick(new RuntimeTickContext(72, 0d, 0d, RuntimeTickStage.PostSimulation));

            Assert.AreEqual(1, bridge.LastFrameRequestCount);
            Assert.AreEqual(1, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(0, bridge.LastFrameFailureCount);
            pending.Clear();
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pending));
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.BeamLanceBeamDefinitionId), pending[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.BeamLanceBeamLifetimeFrames, pending[0].LifetimeFramesOverride);
            Assert.AreEqual(VfxSourceDomains.ActionPresentation, pending[0].SourceDomain);
            Assert.AreEqual(attacker.Id.Value, pending[0].SourceId);
            Assert.AreEqual(30, pending[0].LocalFrame);
        }

[Test]
        public void CharacterActionRuntime_ChannelBeamTimeline_EnqueuesChargeBeamAndTenTickRequests()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.ChannelBeamActionId,
                out CharacterActionConfig action));
            Assert.AreEqual(120, action.DurationFrames);
            Assert.AreEqual(3, action.Phases.Length);
            Assert.AreEqual(0, action.Phases[0].StartFrame);
            Assert.AreEqual(29, action.Phases[0].EndFrame);
            Assert.AreEqual(30, action.Phases[1].StartFrame);
            Assert.AreEqual(89, action.Phases[1].EndFrame);
            Assert.AreEqual(90, action.Phases[2].StartFrame);
            Assert.AreEqual(119, action.Phases[2].EndFrame);
            Assert.AreEqual(2, action.MotionTrack.Events.Length);
            Assert.AreEqual(0, action.MotionTrack.Events[0].Frame);
            Assert.AreEqual(0f, action.MotionTrack.Events[0].MovementControlScale, 0.0001f);
            Assert.AreEqual(0f, action.MotionTrack.Events[0].RotationControlScale, 0.0001f);
            Assert.AreEqual(119, action.MotionTrack.Events[1].Frame);
            Assert.AreEqual(1f, action.MotionTrack.Events[1].MovementControlScale, 0.0001f);
            Assert.AreEqual(1f, action.MotionTrack.Events[1].RotationControlScale, 0.0001f);

            int[] expectedTickFrames = { 30, 36, 42, 48, 54, 60, 66, 72, 78, 84 };
            Assert.AreEqual(expectedTickFrames.Length, action.GameplayTrack.Events.Length);
            for (int i = 0; i < expectedTickFrames.Length; i++)
            {
                GameplayTrackEvent tickEvent = action.GameplayTrack.Events[i];
                Assert.AreEqual(expectedTickFrames[i], tickEvent.Frame);
                Assert.AreEqual(CharacterActionTrackEventKind.SendGameplayRequest, tickEvent.Kind);
                Assert.AreEqual(
                    CharacterTestCharacterActionConfigSource.ChannelBeamProjectileRequestId,
                    tickEvent.RequestId);
                Assert.AreEqual(
                    CharacterTestCharacterActionConfigSource.ChannelBeamProjectileImpactStableEventId,
                    tickEvent.StableEventId);
            }

            Assert.AreEqual(2, action.PresentationTrack.Events.Length);
            PresentationTrackEvent chargeEvent = action.PresentationTrack.Events[0];
            PresentationTrackEvent beamEvent = action.PresentationTrack.Events[1];
            Assert.AreEqual(0, chargeEvent.Frame);
            Assert.AreEqual(29, chargeEvent.EndFrame);
            Assert.AreEqual(CharacterActionTrackEventKind.SpawnVisualCue, chargeEvent.Kind);
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamChargeResourceKey.ToString(), chargeEvent.ResourceKey);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.ChannelBeamChargeVfxStableEventId,
                chargeEvent.StableEventId);
            Assert.AreEqual(30, beamEvent.Frame);
            Assert.AreEqual(119, beamEvent.EndFrame);
            Assert.AreEqual(CharacterActionTrackEventKind.SpawnVisualCue, beamEvent.Kind);
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamBeamResourceKey.ToString(), beamEvent.ResourceKey);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.ChannelBeamBeamVfxStableEventId,
                beamEvent.StableEventId);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.ChannelBeamIntentId,
                RuntimeFrame.Zero));
            Assert.AreEqual(1, attacker.PendingVfxRequestCount);

            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            var bridge = new CharacterTestActionVfxBridgeModule(
                () => world.Entities,
                vfx,
                NullRuntimeLogger.Instance);
            bridge.Tick(new RuntimeTickContext(42, 0d, 0d, RuntimeTickStage.PostSimulation));

            Assert.AreEqual(1, bridge.LastFrameRequestCount);
            Assert.AreEqual(1, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(0, bridge.LastFrameFailureCount);
            var pending = new List<VfxSpawnRequest>();
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pending));
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.ChannelBeamChargeDefinitionId), pending[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamChargeLifetimeFrames, pending[0].LifetimeFramesOverride);
            Assert.AreEqual(VfxSourceDomains.ActionPresentation, pending[0].SourceDomain);
            Assert.AreEqual(attacker.Id.Value, pending[0].SourceId);
            Assert.AreEqual(0, pending[0].LocalFrame);

            for (int i = 0; i < 30; i++)
                runtime.TickEntity(attacker);

            Assert.AreEqual(1, attacker.PendingVfxRequestCount);
            Assert.AreEqual(1, attacker.PendingGameplayRequestCount);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.ChannelBeamProjectileRequestId,
                attacker.GameplayRequests[0].RequestId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.ChannelBeamProjectileImpactStableEventId,
                attacker.GameplayRequests[0].Metadata.StableEventId);

            vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            bridge = new CharacterTestActionVfxBridgeModule(
                () => world.Entities,
                vfx,
                NullRuntimeLogger.Instance);
            bridge.Tick(new RuntimeTickContext(72, 0d, 0d, RuntimeTickStage.PostSimulation));

            Assert.AreEqual(1, bridge.LastFrameRequestCount);
            Assert.AreEqual(1, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(0, bridge.LastFrameFailureCount);
            pending.Clear();
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pending));
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.ChannelBeamBeamDefinitionId), pending[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamBeamLifetimeFrames, pending[0].LifetimeFramesOverride);
            Assert.AreEqual(VfxSourceDomains.ActionPresentation, pending[0].SourceDomain);
            Assert.AreEqual(attacker.Id.Value, pending[0].SourceId);
            Assert.AreEqual(30, pending[0].LocalFrame);
        }

[Test]
        public void CharacterTestVfxAnchorResolver_TargetHitPoseWinsOverTargetEntityAnchor()
        {
            var resolver = new CharacterTestVfxAnchorResolver(null);
            var pose = new VfxPose(
                VfxVector3.FromRaw(ToVfxRaw(1.25f), ToVfxRaw(0.75f), ToVfxRaw(3.5f)),
                VfxVector3.FromRaw(ToVfxRaw(0f), ToVfxRaw(0f), ToVfxRaw(-1f)));

            Assert.IsTrue(resolver.TryResolve(VfxAnchor.Target(11, 22, pose), out UnityVfxResolvedAnchor resolved));
            Assert.AreEqual(1.25f, resolved.Position.x, 0.0001f);
            Assert.AreEqual(0.75f, resolved.Position.y, 0.0001f);
            Assert.AreEqual(3.5f, resolved.Position.z, 0.0001f);
            Assert.AreEqual(0f, resolved.Forward.x, 0.0001f);
            Assert.AreEqual(0f, resolved.Forward.y, 0.0001f);
            Assert.AreEqual(-1f, resolved.Forward.z, 0.0001f);
            Assert.IsTrue(
                resolver.TryResolve(
                    VfxAnchor.Target(0, 22, pose),
                    out UnityVfxResolvedAnchor sourceLessResolved));
            Assert.AreEqual(1.25f, sourceLessResolved.Position.x, 0.0001f);
            Assert.AreEqual(0.75f, sourceLessResolved.Position.y, 0.0001f);
            Assert.AreEqual(3.5f, sourceLessResolved.Position.z, 0.0001f);
            Assert.IsFalse(resolver.TryResolve(VfxAnchor.Source(11, pose), out _));
            Assert.IsFalse(resolver.TryResolve(VfxAnchor.Target(22, 22, pose), out _));
        }

[Test]
        public void CharacterActionRuntime_GuardAuraVfx_EnqueuesGroundCenteredAuraSpawn()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity attacker);
            attacker.SetTransform(new CharacterTestCharacterTransformComponent(2f, 3f, 4f, 90f));
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var actionSystem = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime);
            abilityRuntime.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PreSimulation));
            attacker.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.zero,
                jumpPressed: false,
                abilitySlotPressed: 8,
                abilityIdPressed: CharacterTestAbilityIds.GuardAuraAbilityId));
            QueueGameplayIntent(
                attacker,
                CharacterTestGameplayIntentKind.AbilitySlotCast,
                RuntimeFrame.Zero,
                abilitySlotPressed: 8,
                abilityIdPressed: CharacterTestAbilityIds.GuardAuraAbilityId);

            actionSystem.Tick(world, RuntimeFrame.Zero);

            Assert.AreEqual(CharacterTestCharacterActionConfigSource.GuardAuraActionId, attacker.Action.ActiveActionId);
            Assert.AreEqual(1, attacker.PendingVfxRequestCount);

            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            var bridge = new CharacterTestActionVfxBridgeModule(
                () => world.Entities,
                vfx,
                NullRuntimeLogger.Instance);
            bridge.Tick(new RuntimeTickContext(42, 0d, 0d, RuntimeTickStage.PostSimulation));

            Assert.AreEqual(1, bridge.LastFrameRequestCount);
            Assert.AreEqual(1, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(0, bridge.LastFrameFailureCount);
            Assert.AreEqual(0, attacker.PendingVfxRequestCount);

            var pending = new List<VfxSpawnRequest>();
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pending));
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.GuardAuraDefinitionId), pending[0].DefinitionId);
            Assert.AreEqual(VfxSourceDomains.ActionPresentation, pending[0].SourceDomain);
            Assert.AreEqual(42, pending[0].SourceFrame);
            Assert.AreEqual(0, pending[0].LocalFrame);
            Assert.AreEqual(attacker.Id.Value, pending[0].SourceId);
            Assert.AreEqual(CharacterTestVfxIds.GuardAuraLifetimeFrames, pending[0].LifetimeFramesOverride);
            Assert.AreEqual(VfxAnchorKind.SourceEntity, pending[0].Anchor.Kind);
            Assert.AreEqual(attacker.Id.Value, pending[0].Anchor.SourceId);
            AssertVfxVector(2f, 3f, 4f, pending[0].Anchor.Pose.Position);
            AssertVfxVector(1f, 0f, 0f, pending[0].Anchor.Pose.Forward);
        }

[Test]
        public void CharacterActionRuntime_FireballBurstProjectile_AreaDamagesMultipleEnemies()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.FireballBurstIntentId,
                RuntimeFrame.Zero));

            List<HitResolveResult> results = RunUntilCombatResults(
                world,
                runtime,
                motion,
                combat,
                attacker,
                expectedAcceptedDamageCount: 2,
                maxFrames: 20,
                startFrame: 0);

            Assert.AreEqual(2, results.Count(result => result.IsAcceptedDamage));
            Assert.Less(world.GetCurrentAttribute(firstTarget, CharacterTestCharacterAttributeIds.Hp), CharacterTestCharacterAttributeIds.DefaultMaxHp);
            Assert.Less(world.GetCurrentAttribute(secondTarget, CharacterTestCharacterAttributeIds.Hp), CharacterTestCharacterAttributeIds.DefaultMaxHp);
            Assert.That(combat.DamageTelemetryRows, Has.Some.Matches<DamageTelemetryRow>(row =>
                row.SourceKind == DamageProfileSourceKind.ProjectileImpact
                && row.ActionId == CharacterTestCharacterActionConfigSource.FireballBurstActionId
                && row.StableEventId == CharacterTestCharacterActionConfigSource.FireballBurstProjectileImpactStableEventId
                && (row.DamageProfileId == 0)));
            Assert.That(combat.HitPresentationEvents, Has.Some.Matches<HitPresentationEvent>(evt =>
                evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.FireballBurstPresentationProfileId));
            AssertOnHitDefenseBreakBuff(world, firstTarget, results[0].TraceId);
            AssertOnHitDefenseBreakBuff(world, secondTarget, results[0].TraceId);
            Assert.AreEqual(2, combat.OnHitBuffLastSummary.RequestedBuffCount);
        }

[Test]
        public void CharacterActionRuntime_FlameLanceProjectile_PiercesMultipleEnemiesOnceEach()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            AlignTargetsWithDefaultProjectileSocketLine(firstTarget, secondTarget);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.FlameLanceIntentId,
                RuntimeFrame.Zero));

            List<HitResolveResult> results = RunUntilCombatResults(
                world,
                runtime,
                motion,
                combat,
                attacker,
                expectedAcceptedDamageCount: 2,
                maxFrames: 20,
                startFrame: 0);

            Assert.AreEqual(2, results.Count(result => result.IsAcceptedDamage));
            Assert.AreEqual(2, results.Select(result => result.TargetId.Value).Distinct().Count());
            Assert.Less(world.GetCurrentAttribute(firstTarget, CharacterTestCharacterAttributeIds.Hp), CharacterTestCharacterAttributeIds.DefaultMaxHp);
            Assert.Less(world.GetCurrentAttribute(secondTarget, CharacterTestCharacterAttributeIds.Hp), CharacterTestCharacterAttributeIds.DefaultMaxHp);
            Assert.That(combat.DamageTelemetryRows, Has.Exactly(2).Matches<DamageTelemetryRow>(row =>
                row.SourceKind == DamageProfileSourceKind.ProjectileImpact
                && row.ActionId == CharacterTestCharacterActionConfigSource.FlameLanceActionId
                && row.StableEventId == CharacterTestCharacterActionConfigSource.FlameLanceProjectileImpactStableEventId
                && (row.DamageProfileId == 0)));
            CombatProjectileRuntimeState state = combat.CreateProjectileStateSnapshot();
            Assert.AreEqual(1, state.ActiveCount);
            Assert.AreEqual(2, state.ActiveProjectiles[0].AcceptedImpactCount);
            Assert.That(combat.HitPresentationEvents, Has.Exactly(2).Matches<HitPresentationEvent>(evt =>
                evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.FlameLancePresentationProfileId));
        }

[Test]
        public void CharacterActionRuntime_BeamLanceProjectile_OneFrameSweepPiercesAndExpires()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            AlignTargetsWithDefaultProjectileSocketLine(firstTarget, secondTarget);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.BeamLanceIntentId,
                RuntimeFrame.Zero));

            var results = new List<HitResolveResult>();
            CombatProjectileSnapshot spawnedProjectile = default;
            bool sawBeamProjectile = false;
            bool placedTargets = false;
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            for (int i = 0; i < 50; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                if (sawBeamProjectile && !placedTargets)
                {
                    PlaceTargetOnProjectileLine(physicsRuntime, firstTarget, spawnedProjectile, Fix64.FromInt(6));
                    PlaceTargetOnProjectileLine(physicsRuntime, secondTarget, spawnedProjectile, Fix64.FromInt(12));
                    placedTargets = true;
                }

                combat.Tick(world, runtime, frame);
                if (!sawBeamProjectile)
                {
                    CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                    if (projectileState.ActiveProjectiles.Count > 0)
                    {
                        for (int projectileIndex = 0; projectileIndex < projectileState.ActiveProjectiles.Count; projectileIndex++)
                        {
                            CombatProjectileSnapshot candidate = projectileState.ActiveProjectiles[projectileIndex];
                            if (candidate.DefinitionId != CharacterTestFireballProjectileAdapter.BeamLanceDefinitionId)
                                continue;

                            spawnedProjectile = candidate;
                            sawBeamProjectile = true;
                            break;
                        }
                    }
                }

                if (attacker.LastCombatHitResults.Count > 0)
                    results.AddRange(attacker.LastCombatHitResults);
                if (results.Count(result => result.IsAcceptedDamage) >= 2)
                    break;
            }

            Assert.IsTrue(sawBeamProjectile, "Expected Beam Lance projectile to spawn.");
            Assert.IsTrue(placedTargets, "Expected test targets to be placed on the Beam Lance sweep line.");
            Assert.AreEqual(2, results.Count(result => result.IsAcceptedDamage));
            Assert.AreEqual(2, results.Select(result => result.TargetId.Value).Distinct().Count());
            Assert.Less(world.GetCurrentAttribute(firstTarget, CharacterTestCharacterAttributeIds.Hp), CharacterTestCharacterAttributeIds.DefaultMaxHp);
            Assert.Less(world.GetCurrentAttribute(secondTarget, CharacterTestCharacterAttributeIds.Hp), CharacterTestCharacterAttributeIds.DefaultMaxHp);
            CombatEntityId firstTargetId = physicsRuntime.GetEntityId(firstTarget);
            CombatEntityId secondTargetId = physicsRuntime.GetEntityId(secondTarget);
            HitResolveResult firstAcceptedHit = results.First(result =>
                result.IsAcceptedDamage && result.TargetId.Equals(firstTargetId));
            HitResolveResult secondAcceptedHit = results.First(result =>
                result.IsAcceptedDamage && result.TargetId.Equals(secondTargetId));
            AssertStasisBuff(
                world,
                firstTarget,
                firstAcceptedHit.TraceId,
                firstAcceptedHit.Frame.Value + CharacterTestStasisIds.StasisBuffDurationFrames);
            AssertStasisBuff(
                world,
                secondTarget,
                secondAcceptedHit.TraceId,
                secondAcceptedHit.Frame.Value + CharacterTestStasisIds.StasisBuffDurationFrames);
            Assert.That(combat.DamageTelemetryRows, Has.Exactly(2).Matches<DamageTelemetryRow>(row =>
                row.SourceKind == DamageProfileSourceKind.ProjectileImpact
                && row.ActionId == CharacterTestCharacterActionConfigSource.BeamLanceActionId
                && row.StableEventId == CharacterTestCharacterActionConfigSource.BeamLanceProjectileImpactStableEventId
                && (row.DamageProfileId == 0)));
            CombatProjectileRuntimeState state = combat.CreateProjectileStateSnapshot();
            Assert.AreEqual(0, state.ActiveCount);
            Assert.That(combat.HitPresentationEvents, Has.Exactly(2).Matches<HitPresentationEvent>(evt =>
                evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.BeamLancePresentationProfileId));
        }

[Test]
        public void CharacterActionRuntime_LaserLanceProjectile_BlocksOnFirstHitAndRecordsPointToPointPath()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            AlignTargetsWithDefaultProjectileSocketLine(firstTarget, secondTarget);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.LaserLanceIntentId,
                RuntimeFrame.Zero));

            var results = new List<HitResolveResult>();
            CombatProjectileSnapshot spawnedProjectile = default;
            CharacterTestLaserPathVfxEvent pathEvent = default;
            bool sawLaserProjectile = false;
            bool placedTargets = false;
            bool sawPathEvent = false;
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            for (int i = 0; i < 50; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                if (sawLaserProjectile && !placedTargets)
                {
                    PlaceTargetOnProjectileLine(physicsRuntime, firstTarget, spawnedProjectile, Fix64.FromInt(6));
                    PlaceTargetOnProjectileLine(physicsRuntime, secondTarget, spawnedProjectile, Fix64.FromInt(12));
                    placedTargets = true;
                }

                combat.Tick(world, runtime, frame);
                if (!sawLaserProjectile)
                {
                    CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                    for (int projectileIndex = 0; projectileIndex < projectileState.ActiveProjectiles.Count; projectileIndex++)
                    {
                        CombatProjectileSnapshot candidate = projectileState.ActiveProjectiles[projectileIndex];
                        if (candidate.DefinitionId != CharacterTestFireballProjectileAdapter.LaserLanceDefinitionId)
                            continue;

                        spawnedProjectile = candidate;
                        sawLaserProjectile = true;
                        break;
                    }
                }

                if (attacker.LastCombatHitResults.Count > 0)
                    results.AddRange(attacker.LastCombatHitResults);
                if (combat.LastFrameLaserPathVfxEvents.Count > 0)
                {
                    pathEvent = combat.LastFrameLaserPathVfxEvents[0];
                    sawPathEvent = true;
                }

                if (sawPathEvent)
                    break;
            }

            Assert.IsTrue(sawLaserProjectile, "Expected Laser Lance projectile to spawn.");
            Assert.IsTrue(placedTargets, "Expected test targets to be placed on the Laser Lance line.");
            Assert.AreEqual(1, results.Count(result => result.IsAcceptedDamage));
            HitResolveResult acceptedHit = results.First(result => result.IsAcceptedDamage);
            Assert.AreEqual(physicsRuntime.GetEntityId(firstTarget), acceptedHit.TargetId);
            AssertNoStasisBuff(world, firstTarget);
            AssertNoStasisBuff(world, secondTarget);
            Assert.IsTrue(sawPathEvent, "Expected Laser Lance path VFX event.");
            Assert.IsTrue(pathEvent.Hit);
            Assert.AreEqual(firstTarget.Id.Value, pathEvent.TargetEntityId);
            Assert.AreEqual(spawnedProjectile.Origin, pathEvent.Origin);
            Assert.AreEqual(acceptedHit.HitPointWorld, pathEvent.Endpoint);
            Assert.AreEqual(CharacterTestFireballProjectileAdapter.LaserLanceDefinitionId, spawnedProjectile.DefinitionId);
            Assert.That(combat.HitPresentationEvents, Has.Exactly(1).Matches<HitPresentationEvent>(evt =>
                evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.LaserLancePresentationProfileId));
        }

[Test]
        public void CharacterActionRuntime_PoisonBottleProjectile_CreatesPoolRefreshesPoisonedAndContinuesTicksWhileActive()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AlignTargetWithDefaultProjectileSocketLine(target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.PoisonBottleIntentId,
                RuntimeFrame.Zero));

            int directProjectileDamageCount = 0;
            long poolFrame = -1L;
            CharacterTestPoisonBottlePoolTickSummary poolSummary = default;
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            int poolSpawnTimeoutFrame = CharacterTestFireballProjectileAdapter.PoisonBottleLifetimeFrames + 60;
            for (int frameValue = 0; frameValue < poolSpawnTimeoutFrame; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                CharacterTestBattleStateSystem.RefreshWorld(world, frame, null);
                combat.Tick(world, runtime, frame);
                directProjectileDamageCount += combat.DamageTelemetryRows.Count(row =>
                    row.SourceKind == DamageProfileSourceKind.ProjectileImpact
                    && row.ActionId == CharacterTestCharacterActionConfigSource.PoisonBottleActionId);
                if (combat.PoisonBottlePoolLastTickSummary.CreatedPoolCount <= 0)
                    continue;

                poolFrame = frameValue;
                poolSummary = combat.PoisonBottlePoolLastTickSummary;
                break;
            }

            Assert.GreaterOrEqual(poolFrame, 0L, "Poison Bottle should create a pool from the projectile hit.");
            Assert.AreEqual(1, poolSummary.CreatedPoolCount);
            Assert.AreEqual(1, poolSummary.ActivePoolCount);
            Assert.AreEqual(1, poolSummary.QueryCount);
            Assert.GreaterOrEqual(poolSummary.QueryHitCount, 1);
            Assert.AreEqual(1, poolSummary.AppliedOrRefreshedCount);
            Assert.AreEqual(0, directProjectileDamageCount);
            Assert.AreEqual(100, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
            AssertPoisonedBuff(
                world,
                target,
                CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                poolFrame,
                poolFrame + CharacterTestPoisonIds.PoisonedBuffDurationFrames);
            AssertNoStasisBuff(world, target);
            Assert.AreEqual(0, combat.OnHitBuffLastSummary.RequestedBuffCount);

            int tickDamageCount = 0;
            int[] tickFrames = new int[8];
            int tickFrameIndex = 0;
            int[] poolRefreshFrames = new int[5];
            int poolRefreshFrameIndex = 0;
            bool poolExpired = false;
            bool sawTickPresentation = false;
            long firstTickFrame = poolFrame + 60L;
            long extendedPoisonEndFrame = poolFrame + 150L + CharacterTestPoisonIds.PoisonedBuffDurationFrames;
            long finalTickFrame = poolFrame + 480L;
            for (long frameValue = poolFrame + 1L; frameValue <= finalTickFrame + 5L; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                CharacterTestBattleStateSystem.RefreshWorld(world, frame, null);
                combat.Tick(world, runtime, frame);

                CharacterTestPoisonBottlePoolTickSummary tickSummary =
                    combat.PoisonBottlePoolLastTickSummary;
                if (tickSummary.AppliedOrRefreshedCount > 0 && poolRefreshFrameIndex < poolRefreshFrames.Length)
                    poolRefreshFrames[poolRefreshFrameIndex++] = (int)frameValue;
                if (tickSummary.ExpiredPoolCount > 0)
                    poolExpired = true;
                if (combat.LastPoisonTickTargetCount > 0 && tickFrameIndex < tickFrames.Length)
                    tickFrames[tickFrameIndex++] = (int)frameValue;

                tickDamageCount += combat.DamageTelemetryRows.Count(row =>
                    row.SourceKind == DamageProfileSourceKind.StatusTick
                    && !row.ActionId.IsValid
                    && row.StableEventId == CharacterTestCharacterActionConfigSource.PoisonTickStableEventId
                    && row.TargetEntityId == target.Id.Value
                    && row.DamageTypeId == CharacterTestAuthoredContentIds.D409_Id10
                    && row.FinalDamage == 2);
                sawTickPresentation |= combat.HitPresentationEvents.Any(evt =>
                    evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.PoisonTickPresentationProfileId);
            }

            Assert.AreEqual(5, poolRefreshFrameIndex);
            CollectionAssert.AreEqual(
                new[]
                {
                    (int)(poolFrame + 30L),
                    (int)(poolFrame + 60L),
                    (int)(poolFrame + 90L),
                    (int)(poolFrame + 120L),
                    (int)(poolFrame + 150L)
                },
                poolRefreshFrames);
            Assert.IsTrue(poolExpired, "Poison Bottle pool should expire after its 180-frame duration.");
            Assert.AreEqual(0, combat.ActivePoisonBottlePoolCount);
            AssertPoisonedBuff(
                world,
                target,
                CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                poolFrame,
                poolFrame + 150L + CharacterTestPoisonIds.PoisonedBuffDurationFrames);
            Assert.AreEqual(8, tickFrameIndex);
            CollectionAssert.AreEqual(
                new[]
                {
                    (int)firstTickFrame,
                    (int)(firstTickFrame + 60L),
                    (int)(firstTickFrame + 120L),
                    (int)(firstTickFrame + 180L),
                    (int)(firstTickFrame + 240L),
                    (int)(firstTickFrame + 300L),
                    (int)(firstTickFrame + 360L),
                    (int)finalTickFrame
                },
                tickFrames);
            Assert.Less(finalTickFrame, extendedPoisonEndFrame);
            Assert.AreEqual(8, tickDamageCount);
            Assert.AreEqual(84, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
            Assert.IsTrue(sawTickPresentation, "Poison tick damage should publish hit presentation.");
        }

[Test]
        public void CharacterActionRuntime_ChainLightning_ResolvesLockedTargetThenJumpsFourHostiles()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity closerForwardTarget;
            CharacterTestCharacterEntity lockedTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out closerForwardTarget,
                out lockedTarget);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity thirdTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    2,
                    1f,
                    0f,
                    12.5f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity fourthTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    2,
                    -1f,
                    0f,
                    16f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity cappedTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    2,
                    -1f,
                    0f,
                    19.5f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            closerForwardTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 6f, 180f));
            lockedTarget.SetTransform(new CharacterTestCharacterTransformComponent(1f, 0f, 8f, 180f));
            thirdTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            fourthTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            cappedTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => lockedTarget.Id.Value);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.ChainLightningIntentId,
                RuntimeFrame.Zero));

            var results = new List<HitResolveResult>();
            IReadOnlyList<CharacterTestChainLightningVfxEvent> arcEvents =
                Array.Empty<CharacterTestChainLightningVfxEvent>();
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            for (int i = 0; i < 50; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);

                if (attacker.LastCombatHitResults.Count > 0)
                    results.AddRange(attacker.LastCombatHitResults);
                if (combat.LastFrameChainLightningVfxEvents.Count > 0)
                    arcEvents = combat.LastFrameChainLightningVfxEvents.ToArray();
                if (results.Count(result => result.IsAcceptedDamage) >= 4)
                    break;
            }

            Assert.AreEqual(4, results.Count(result => result.IsAcceptedDamage));
            Assert.AreEqual(4, results.Where(result => result.IsAcceptedDamage).Select(result => result.TargetId.Value).Distinct().Count());
            Assert.AreEqual(4, combat.LastChainLightningTargetCount);
            Assert.AreEqual(4, arcEvents.Count);
            Assert.AreEqual(lockedTarget.Id.Value, arcEvents[0].TargetEntityId);
            CollectionAssert.AreEqual(new[] { 6, 5, 4, 3 }, combat.DamageTelemetryRows
                .Where(row => row.ActionId == CharacterTestCharacterActionConfigSource.ChainLightningActionId)
                .Select(row => row.IncomingDamage)
                .ToArray());
            Assert.That(combat.DamageTelemetryRows, Has.Exactly(4).Matches<DamageTelemetryRow>(row =>
                row.SourceKind == DamageProfileSourceKind.Scripted
                && row.ActionId == CharacterTestCharacterActionConfigSource.ChainLightningActionId
                && row.StableEventId == CharacterTestCharacterActionConfigSource.ChainLightningImpactStableEventId
                && row.AnalysisCategory == CharacterTestAuthoredContentIds.D411_Id9
                && row.DamageTypeId == CharacterTestAuthoredContentIds.D409_Id9
                && row.ImpactPower == 2));
            Assert.That(combat.HitPresentationEvents, Has.Exactly(4).Matches<HitPresentationEvent>(evt =>
                evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.ChainLightningPresentationProfileId));
            AssertNoStasisBuff(world, closerForwardTarget);
            AssertNoStasisBuff(world, lockedTarget);
            AssertNoStasisBuff(world, thirdTarget);
            AssertNoStasisBuff(world, fourthTarget);
            AssertNoStasisBuff(world, cappedTarget);
            Assert.Less(world.GetCurrentAttribute(closerForwardTarget, CharacterTestCharacterAttributeIds.Hp), CharacterTestCharacterAttributeIds.DefaultMaxHp);
            Assert.Less(world.GetCurrentAttribute(lockedTarget, CharacterTestCharacterAttributeIds.Hp), CharacterTestCharacterAttributeIds.DefaultMaxHp);
            Assert.Less(world.GetCurrentAttribute(thirdTarget, CharacterTestCharacterAttributeIds.Hp), CharacterTestCharacterAttributeIds.DefaultMaxHp);
            Assert.Less(world.GetCurrentAttribute(fourthTarget, CharacterTestCharacterAttributeIds.Hp), CharacterTestCharacterAttributeIds.DefaultMaxHp);
            Assert.AreEqual(
                CharacterTestCharacterAttributeIds.DefaultMaxHp,
                world.GetCurrentAttribute(cappedTarget, CharacterTestCharacterAttributeIds.Hp));
        }

[Test]
        public void CharacterActionRuntime_TornadoProjectile_PullsHostileTargetsWhileActive()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(2.25f, 0f, 1.6f, 180f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            var interactionPhysics = new CharacterTestInteractionPhysicsSystem();

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.TornadoIntentId,
                RuntimeFrame.Zero));

            float targetStartX = target.Transform.X;
            bool sawActiveTornado = false;
            bool sawTornadoField = false;
            bool sawAttractionRequest = false;
            bool targetPulledTowardTornado = false;
            for (int i = 0; i < 90; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
                interactionPhysics.Tick(world, physicsRuntime, combat.LastFrameInteractionEvents, frame);

                CombatProjectileRuntimeState state = combat.CreateProjectileStateSnapshot();
                sawActiveTornado |= state != null
                    && state.ActiveProjectiles.Any(projectile =>
                        projectile.DefinitionId == CharacterTestFireballProjectileAdapter.TornadoDefinitionId);
                sawTornadoField |= combat.LastFrameInteractionEvents.Any(evt =>
                    evt.Kind == CharacterTestInteractionKind.PhysicalField
                    && evt.StableId == CharacterTestCharacterActionConfigSource.TornadoAttractionStableEventId
                    && evt.PhysicalField.Shape == CharacterTestInteractionFieldShape.Capsule
                    && evt.PhysicalField.ForceMode == CharacterTestInteractionForceMode.AttractionAcceleration);
                if (interactionPhysics.LastSummary.CharacterTargetCount > 0)
                {
                    sawAttractionRequest |= target.MotionRequests.Any(request =>
                        request.Kind == CharacterTestCharacterMotionRequestKind.ExternalForce
                        && request.SourceActionId == CharacterTestCharacterActionConfigSource.TornadoActionId
                        && request.StableEventId == CharacterTestCharacterActionConfigSource.TornadoAttractionStableEventId
                        && request.BypassesGroundDamping
                        && request.XRaw < 0);
                }

                targetPulledTowardTornado |= target.Transform.X < targetStartX - 0.01f;
                if (sawActiveTornado && sawAttractionRequest && targetPulledTowardTornado)
                    break;
            }

            Assert.IsTrue(sawActiveTornado, "Tornado projectile did not become active.");
            Assert.IsTrue(sawTornadoField, "Tornado did not publish a PhysicalField attraction interaction.");
            Assert.IsTrue(sawAttractionRequest, "Tornado did not enqueue a hostile-target external force request.");
            Assert.IsTrue(targetPulledTowardTornado, "Tornado external force did not move the side target toward the projectile path.");
        }

[Test]
        public void CharacterActionRuntime_TornadoProjectile_AttractionScalesByDeterministicMass()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity lightTarget;
            CharacterTestCharacterEntity heavyTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out lightTarget,
                out heavyTarget);
            lightTarget.SetTransform(new CharacterTestCharacterTransformComponent(2.25f, 0f, 1.6f, 180f));
            heavyTarget.SetTransform(new CharacterTestCharacterTransformComponent(-2.25f, 0f, 1.6f, 180f));
            lightTarget.SetPhysics(CreateTestPhysicsProfile(massKg: 40f));
            heavyTarget.SetPhysics(CreateTestPhysicsProfile(massKg: 160f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            var interactionPhysics = new CharacterTestInteractionPhysicsSystem();

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.TornadoIntentId,
                RuntimeFrame.Zero));

            bool foundComparableForce = false;
            int lightForceX = 0;
            int heavyForceX = 0;
            for (int i = 0; i < 45; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
                interactionPhysics.Tick(world, physicsRuntime, combat.LastFrameInteractionEvents, frame);

                if (TryGetTornadoAttractionRequest(lightTarget, out CharacterTestCharacterMotionRequest lightRequest)
                    && TryGetTornadoAttractionRequest(heavyTarget, out CharacterTestCharacterMotionRequest heavyRequest))
                {
                    lightForceX = Math.Abs(lightRequest.XRaw);
                    heavyForceX = Math.Abs(heavyRequest.XRaw);
                    foundComparableForce = true;
                    break;
                }
            }

            Assert.IsTrue(foundComparableForce, "Tornado did not enqueue comparable attraction requests for both mass variants.");
            Assert.Greater(lightForceX, 0);
            Assert.Greater(heavyForceX, 0);
            Assert.GreaterOrEqual(
                lightForceX,
                heavyForceX * 3,
                "A 40kg target should receive substantially stronger deterministic attraction acceleration than a 160kg target.");
        }

[Test]
        public void CharacterTestDynamicFieldTargetIndex_TornadoCapsuleMatchesCombatQueryForDynamicTargets()
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity attacker = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity insideTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 2.25f,
                    y: 0f,
                    z: 1.6f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity boundaryTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity verticalMissTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 9f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity missingColliderTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: -1f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                    "movable.index.aabb",
                    501,
                    "movable-natural-object",
                    massKg: 80f,
                    CombatSurfaceMaterial.Default,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(-1.5f, 0.25f, 1f)),
                CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                    "movable.index.sphere",
                    502,
                    "movable-natural-object",
                    massKg: 80f,
                    CombatSurfaceMaterial.Default,
                    new Vector3(0.4f, 0.4f, 0.4f),
                    new Vector3(3.8f, 0.4f, -1.25f),
                    CharacterTestMovableNaturalObjectShapeKind.Sphere),
                CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                    "movable.index.capsule.miss",
                    503,
                    "movable-natural-object",
                    massKg: 80f,
                    CombatSurfaceMaterial.Default,
                    new Vector3(0.35f, 0.9f, 0.35f),
                    new Vector3(8f, 0.9f, 0f),
                    CharacterTestMovableNaturalObjectShapeKind.Capsule)
            });
            CharacterTestInteractionEvent interaction = CreateTornadoAttractionInteraction(
                RuntimeFrame.Zero,
                FixVector3.Zero,
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One));
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            Fix64 boundaryX = interaction.Radius + physicsRuntime.GetCharacterHalfExtents(boundaryTarget).X;
            boundaryTarget.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                CharacterTestDeterministicUnits.Fix64MetersToMillimeters(boundaryX),
                0,
                0));
            SyncDynamicCombatCollidersForTest(world, physicsRuntime);
            physicsRuntime.World.RemoveCollider(
                physicsRuntime.GetBodyId(missingColliderTarget),
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicCharacterColliderId));

            CombatPhysicsLayerMask layerMask = CreateTornadoDynamicLayerMaskForTest();
            CombatQueryHeader header = CreateTornadoQueryHeaderForTest(interaction, layerMask);
            GetTornadoCapsuleForTest(interaction, out FixVector3 pointA, out FixVector3 pointB);
            var oldResults = new List<CombatQueryResult>(8);
            var indexedResults = new List<CombatQueryResult>(8);
            physicsRuntime.World.QueryCapsule(
                new CombatCapsuleQuery(header, pointA, pointB, interaction.Radius),
                oldResults,
                includeSourceEntity: false);

            var index = new CharacterTestDynamicFieldTargetIndex();
            index.Rebuild(world, physicsRuntime, layerMask);
            index.QueryVerticalCapsule(
                header,
                pointA,
                pointB,
                interaction.Radius,
                indexedResults,
                includeSourceEntity: false);

            CollectionAssert.AreEqual(ResultBodyIds(oldResults), ResultBodyIds(indexedResults));
            CollectionAssert.Contains(ResultBodyIds(indexedResults), physicsRuntime.GetBodyId(insideTarget).Value);
            CollectionAssert.Contains(ResultBodyIds(indexedResults), physicsRuntime.GetBodyId(boundaryTarget).Value);
            CollectionAssert.Contains(ResultBodyIds(indexedResults), physicsRuntime.GetMovableObjectBodyId(501).Value);
            CollectionAssert.Contains(ResultBodyIds(indexedResults), physicsRuntime.GetMovableObjectBodyId(502).Value);
            CollectionAssert.DoesNotContain(ResultBodyIds(indexedResults), physicsRuntime.GetBodyId(attacker).Value);
            CollectionAssert.DoesNotContain(ResultBodyIds(indexedResults), physicsRuntime.GetBodyId(verticalMissTarget).Value);
            CollectionAssert.DoesNotContain(ResultBodyIds(indexedResults), physicsRuntime.GetBodyId(missingColliderTarget).Value);
            CollectionAssert.DoesNotContain(ResultBodyIds(indexedResults), physicsRuntime.GetMovableObjectBodyId(503).Value);
        }

[Test]
        public void CharacterTestDynamicFieldTargetIndex_TornadoCapsuleQueryOrderIsStable()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            firstTarget.SetTransform(new CharacterTestCharacterTransformComponent(1f, 0f, 0f, 0f));
            secondTarget.SetTransform(new CharacterTestCharacterTransformComponent(-1f, 0f, 0f, 0f));
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                    "movable.index.stable.a",
                    601,
                    "movable-natural-object",
                    massKg: 80f,
                    CombatSurfaceMaterial.Default,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(0f, 0.25f, 1f)),
                CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                    "movable.index.stable.b",
                    602,
                    "movable-natural-object",
                    massKg: 80f,
                    CombatSurfaceMaterial.Default,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(0f, 0.25f, -1f))
            });
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            SyncDynamicCombatCollidersForTest(world, physicsRuntime);
            CharacterTestInteractionEvent interaction = CreateTornadoAttractionInteraction(
                RuntimeFrame.Zero,
                FixVector3.Zero,
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One));
            CombatPhysicsLayerMask layerMask = CreateTornadoDynamicLayerMaskForTest();
            CombatQueryHeader header = CreateTornadoQueryHeaderForTest(interaction, layerMask);
            GetTornadoCapsuleForTest(interaction, out FixVector3 pointA, out FixVector3 pointB);
            var index = new CharacterTestDynamicFieldTargetIndex();
            var firstResults = new List<CombatQueryResult>(8);
            var secondResults = new List<CombatQueryResult>(8);

            index.Rebuild(world, physicsRuntime, layerMask);
            index.QueryVerticalCapsule(
                header,
                pointA,
                pointB,
                interaction.Radius,
                firstResults,
                includeSourceEntity: false);
            index.QueryVerticalCapsule(
                header,
                pointA,
                pointB,
                interaction.Radius,
                secondResults,
                includeSourceEntity: false);

            CollectionAssert.AreEqual(ResultBodyIds(firstResults), ResultBodyIds(secondResults));
            int[] bodyIds = ResultBodyIds(firstResults);
            int previous = int.MinValue;
            for (int i = 0; i < bodyIds.Length; i++)
            {
                Assert.Greater(bodyIds[i], previous);
                previous = bodyIds[i];
            }
        }

[Test]
        public void CharacterTestDynamicFieldTargetIndex_TornadoCapsuleQueryDoesNotAllocateAfterWarmup()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            firstTarget.SetTransform(new CharacterTestCharacterTransformComponent(2.25f, 0f, 1.6f, 180f));
            secondTarget.SetTransform(new CharacterTestCharacterTransformComponent(-2.25f, 0f, 1.6f, 180f));
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                    "movable.index.alloc",
                    701,
                    "movable-natural-object",
                    massKg: 80f,
                    CombatSurfaceMaterial.Default,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(0f, 0.25f, -1f))
            });
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            SyncDynamicCombatCollidersForTest(world, physicsRuntime);
            CharacterTestInteractionEvent interaction = CreateTornadoAttractionInteraction(
                RuntimeFrame.Zero,
                FixVector3.Zero,
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One));
            CombatPhysicsLayerMask layerMask = CreateTornadoDynamicLayerMaskForTest();
            CombatQueryHeader header = CreateTornadoQueryHeaderForTest(interaction, layerMask);
            GetTornadoCapsuleForTest(interaction, out FixVector3 pointA, out FixVector3 pointB);
            var index = new CharacterTestDynamicFieldTargetIndex();
            var results = new List<CombatQueryResult>(8);

            for (int i = 0; i < 8; i++)
            {
                index.Rebuild(world, physicsRuntime, layerMask);
                results.Clear();
                index.QueryVerticalCapsule(
                    header,
                    pointA,
                    pointB,
                    interaction.Radius,
                    results,
                    includeSourceEntity: false);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++)
            {
                index.Rebuild(world, physicsRuntime, layerMask);
                results.Clear();
                index.QueryVerticalCapsule(
                    header,
                    pointA,
                    pointB,
                    interaction.Radius,
                    results,
                    includeSourceEntity: false);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocatedBytes);
        }

[Test]
        public void CharacterTestDynamicFieldTargetIndex_TornadoCapsuleQueryDoesNotAllocateWithPreparedEmptyResultBuffer()
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            var world = new CharacterTestCharacterRuntimeWorld();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);

            const int targetCount = 40;
            for (int i = 0; i < targetCount; i++)
            {
                float x = -3f + (i % 8) * 0.85f;
                float z = -2f + (i / 8) * 0.85f;
                world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        teamId: 2,
                        x: x,
                        y: 0f,
                        z: z,
                        CharacterTestCharacterControlOwner.None),
                    definition);
            }

            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            SyncDynamicCombatCollidersForTest(world, physicsRuntime);
            CharacterTestInteractionEvent interaction = CreateTornadoAttractionInteraction(
                RuntimeFrame.Zero,
                FixVector3.Zero,
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One));
            CombatPhysicsLayerMask layerMask = CreateTornadoDynamicLayerMaskForTest();
            CombatQueryHeader header = CreateTornadoQueryHeaderForTest(interaction, layerMask);
            GetTornadoCapsuleForTest(interaction, out FixVector3 pointA, out FixVector3 pointB);
            var index = new CharacterTestDynamicFieldTargetIndex();
            var results = new List<CombatQueryResult>();

            index.Rebuild(world, physicsRuntime, layerMask);
            index.EnsureResultCapacity(results);
            _ = CombatQueryResultComparer.Instance;

            Assert.Greater(index.TargetCount, 32);
            Assert.GreaterOrEqual(results.Capacity, index.TargetCount);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int added = index.QueryVerticalCapsule(
                header,
                pointA,
                pointB,
                interaction.Radius,
                results,
                includeSourceEntity: false);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(targetCount, added);
            Assert.AreEqual(0, allocatedBytes);
        }

[Test]
        public void CharacterTestDynamicFieldTargetIndex_FirstStressRebuildAndQueryDoesNotAllocate()
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            var world = new CharacterTestCharacterRuntimeWorld();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);

            const int targetCount = 96;
            for (int i = 0; i < targetCount; i++)
            {
                float x = -2.1f + (i % 12) * 0.38f;
                float z = -1.4f + (i / 12) * 0.38f;
                world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        teamId: 2,
                        x: x,
                        y: 0f,
                        z: z,
                        CharacterTestCharacterControlOwner.None),
                    definition);
            }

            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            SyncDynamicCombatCollidersForTest(world, physicsRuntime);
            CharacterTestInteractionEvent interaction = CreateTornadoAttractionInteraction(
                RuntimeFrame.Zero,
                FixVector3.Zero,
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One));
            CombatPhysicsLayerMask layerMask = CreateTornadoDynamicLayerMaskForTest();
            CombatQueryHeader header = CreateTornadoQueryHeaderForTest(interaction, layerMask);
            GetTornadoCapsuleForTest(interaction, out FixVector3 pointA, out FixVector3 pointB);
            var index = new CharacterTestDynamicFieldTargetIndex();
            var results = new List<CombatQueryResult>(targetCount + 1);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            index.Rebuild(world, physicsRuntime, layerMask);
            int added = index.QueryVerticalCapsule(
                header,
                pointA,
                pointB,
                interaction.Radius,
                results,
                includeSourceEntity: false);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.GreaterOrEqual(index.TargetCount, targetCount);
            Assert.Greater(added, 0);
            Assert.AreEqual(0, allocatedBytes);
        }

[Test]
        public void CharacterActionRuntime_TornadoProjectile_ContinuesAfterDirectPathAndAppliesPeriodicDamage()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            firstTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 2.45f, 180f));
            secondTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 5.45f, 180f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.TornadoIntentId,
                RuntimeFrame.Zero));

            int firstStartingHealth = world.GetCurrentAttribute(firstTarget, CharacterTestCharacterAttributeIds.Hp);
            int secondStartingHealth = world.GetCurrentAttribute(secondTarget, CharacterTestCharacterAttributeIds.Hp);
            int tickDamageTelemetryCount = 0;
            bool sawActiveAfterCrossingTarget = false;
            bool sawTickDiagnostic = false;
            bool sawTickPresentation = false;
            for (int i = 0; i < 120; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);

                CombatProjectileRuntimeState state = combat.CreateProjectileStateSnapshot();
                if (i > 75)
                {
                    sawActiveAfterCrossingTarget |= state != null
                        && state.ActiveProjectiles.Any(projectile =>
                            projectile.DefinitionId == CharacterTestFireballProjectileAdapter.TornadoDefinitionId);
                }

                if (combat.LastTornadoDamageTickTargetCount > 0)
                    sawTickDiagnostic = true;

                tickDamageTelemetryCount += combat.DamageTelemetryRows.Count(row =>
                    row.SourceKind == DamageProfileSourceKind.ProjectileImpact
                    && row.ActionId == CharacterTestCharacterActionConfigSource.TornadoActionId
                    && row.StableEventId == CharacterTestCharacterActionConfigSource.TornadoProjectileTickStableEventId
                    && (row.DamageProfileId == 0)
                    && (row.TargetEntityId == firstTarget.Id.Value || row.TargetEntityId == secondTarget.Id.Value)
                    && row.FinalDamage > 0);
                sawTickPresentation |= combat.HitPresentationEvents.Any(evt =>
                    evt.PresentationProfileId == CharacterTestCharacterActionConfigSource.TornadoTickPresentationProfileId);

                if (sawActiveAfterCrossingTarget && tickDamageTelemetryCount >= 2 && sawTickPresentation)
                    break;
            }

            Assert.IsTrue(sawActiveAfterCrossingTarget, "Tornado projectile was consumed by the direct path target.");
            Assert.IsTrue(sawTickDiagnostic, "Tornado tick diagnostic did not report any damaged targets.");
            Assert.GreaterOrEqual(tickDamageTelemetryCount, 2, "Expected at least two Tornado periodic damage ticks.");
            Assert.Less(
                world.GetCurrentAttribute(firstTarget, CharacterTestCharacterAttributeIds.Hp)
                + world.GetCurrentAttribute(secondTarget, CharacterTestCharacterAttributeIds.Hp),
                firstStartingHealth + secondStartingHealth,
                "Tornado periodic damage did not reduce hostile target health.");
            Assert.IsTrue(sawTickPresentation, "Tornado periodic damage did not publish tick hit presentation.");
        }

[Test]
        public void CharacterActionRuntime_TornadoProjectile_ColumnTickHitsElevatedTarget()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 4.75f, 1.5f, 180f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.TornadoIntentId,
                RuntimeFrame.Zero));

            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);

            bool sawColumnTickDamage = false;
            for (int i = 0; i < 75; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                combat.Tick(world, runtime, frame);

                sawColumnTickDamage |= combat.DamageTelemetryRows.Any(row =>
                    row.SourceKind == DamageProfileSourceKind.ProjectileImpact
                    && row.ActionId == CharacterTestCharacterActionConfigSource.TornadoActionId
                    && row.StableEventId == CharacterTestCharacterActionConfigSource.TornadoProjectileTickStableEventId
                    && row.TargetEntityId == target.Id.Value
                    && row.FinalDamage > 0);

                if (sawColumnTickDamage)
                    break;
            }

            Assert.IsTrue(
                sawColumnTickDamage,
                "Tornado tick damage should use a vertical column query that can hit elevated targets above the old spherical tick radius.");
        }

[Test]
        public void CharacterActionRuntime_TornadoProjectile_DamageTickFastPathFiltersTargetsLikeCombatQuery()
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity attacker = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity hostileTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 1.5f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity friendlyTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    x: 0.75f,
                    y: 0f,
                    z: 1.5f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity missingColliderTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: -0.75f,
                    y: 0f,
                    z: 1.5f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            attacker.SetMotionState(0f, Vector2.zero, isGrounded: true);
            hostileTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            friendlyTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            missingColliderTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.TornadoIntentId,
                RuntimeFrame.Zero));

            bool sawHostileDamage = false;
            bool sawFriendlyDamage = false;
            bool sawSourceDamage = false;
            bool sawMissingColliderDamage = false;
            for (int i = 0; i < 75; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                motion.Tick(world, 1f / 60f, frame);
                physicsRuntime.World.RemoveCollider(
                    physicsRuntime.GetBodyId(missingColliderTarget),
                    new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicCharacterColliderId));
                combat.Tick(world, runtime, frame);

                IReadOnlyList<DamageTelemetryRow> rows = combat.DamageTelemetryRows;
                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    DamageTelemetryRow row = rows[rowIndex];
                    if (row.SourceKind != DamageProfileSourceKind.ProjectileImpact
                        || row.ActionId != CharacterTestCharacterActionConfigSource.TornadoActionId
                        || row.StableEventId != CharacterTestCharacterActionConfigSource.TornadoProjectileTickStableEventId
                        || row.FinalDamage <= 0)
                    {
                        continue;
                    }

                    sawHostileDamage |= row.TargetEntityId == hostileTarget.Id.Value;
                    sawFriendlyDamage |= row.TargetEntityId == friendlyTarget.Id.Value;
                    sawSourceDamage |= row.TargetEntityId == attacker.Id.Value;
                    sawMissingColliderDamage |= row.TargetEntityId == missingColliderTarget.Id.Value;
                }

                if (sawHostileDamage)
                    break;
            }

            Assert.IsTrue(sawHostileDamage, "Tornado damage tick should hit hostile dynamic character targets.");
            Assert.IsFalse(sawFriendlyDamage, "Tornado damage tick should keep friendly targets filtered out.");
            Assert.IsFalse(sawSourceDamage, "Tornado damage tick should keep the source entity excluded.");
            Assert.IsFalse(sawMissingColliderDamage, "Tornado damage tick should skip targets with a missing combat collider.");
        }

[Test]
        public void CharacterActionRuntime_TornadoProjectile_DamageTickDoesNotAllocateAfterWarmup()
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity attacker = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            attacker.SetMotionState(0f, Vector2.zero, isGrounded: true);

            const int targetCount = 96;
            for (int i = 0; i < targetCount; i++)
            {
                float x = -2.1f + (i % 12) * 0.38f;
                float z = 0.55f + (i / 12) * 0.32f;
                CharacterTestCharacterEntity target = world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        teamId: 2,
                        x: x,
                        y: 0f,
                        z: z,
                        CharacterTestCharacterControlOwner.None),
                    definition);
                target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            }

            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.TornadoIntentId,
                RuntimeFrame.Zero));

            bool warmedTickDamage = false;
            int frameIndex = 0;
            for (; frameIndex < 90; frameIndex++)
            {
                TickTornadoDamageTestFrame(world, runtime, motion, combat, frameIndex);
                warmedTickDamage |= combat.LastTornadoDamageTickTargetCount > 0;
                if (warmedTickDamage)
                {
                    frameIndex++;
                    break;
                }
            }

            Assert.IsTrue(warmedTickDamage, "Warm-up did not reach a Tornado damage tick.");
            long tickAllocatedBytes = -1L;
            for (; frameIndex < CharacterTestFireballProjectileAdapter.TornadoLifetimeFrames; frameIndex++)
            {
                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                TickTornadoDamageTestFrame(world, runtime, motion, combat, frameIndex);
                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                if (combat.LastTornadoDamageTickTargetCount <= 0)
                    continue;

                tickAllocatedBytes = allocatedBytes;
                break;
            }

            Assert.GreaterOrEqual(tickAllocatedBytes, 0L);
            Assert.AreEqual(0L, tickAllocatedBytes);
        }

[Test]
        public void CharacterActionSystem_DebugPrimaryDoesNotStartFireballBurstAbility()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime);

            Assert.IsTrue(abilityRuntime.Definitions.TryGet(
                CharacterTestAbilityIds.FireballBurstAbilityId,
                out AbilityDefinitionConfig burst));

            abilityRuntime.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PreSimulation));
            attacker.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.zero,
                jumpPressed: false,
                debugPrimaryPressed: true));

            system.Tick(world, RuntimeFrame.Zero);

            Assert.AreNotEqual(
                CharacterTestCharacterActionConfigSource.FireballBurstActionId,
                attacker.Action.ActiveActionId);
            Assert.AreEqual(
                0,
                abilityRuntime.GetRemainingCooldownFrames(attacker.Id.Value, burst, RuntimeFrame.Zero));
        }

[Test]
        public void CharacterActionSystem_DebugSecondaryDoesNotStartFireballLobAbility()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime);

            Assert.IsTrue(abilityRuntime.Definitions.TryGet(
                CharacterTestAbilityIds.FireballLobAbilityId,
                out AbilityDefinitionConfig lob));

            abilityRuntime.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PreSimulation));
            attacker.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.zero,
                jumpPressed: false,
                debugSecondaryPressed: true));

            system.Tick(world, RuntimeFrame.Zero);

            Assert.AreNotEqual(
                CharacterTestCharacterActionConfigSource.FireballLobActionId,
                attacker.Action.ActiveActionId);
            Assert.AreEqual(
                0,
                abilityRuntime.GetRemainingCooldownFrames(attacker.Id.Value, lob, RuntimeFrame.Zero));
        }

[Test]
        public void CharacterActionSystem_SkillSlotsStartProjectileAbilities()
        {
            AssertSkillSlotStartsAbility(
                slotIndex: 0,
                CharacterTestAbilityIds.FireballAbilityId,
                (CharacterTestCharacterActionConfigSource.FireballActionId).ToString(),
                CharacterTestAbilityIds.FireballCooldownFrames);
            AssertSkillSlotStartsAbility(
                slotIndex: 3,
                CharacterTestAbilityIds.FireballLobAbilityId,
                (CharacterTestCharacterActionConfigSource.FireballLobActionId).ToString(),
                CharacterTestAbilityIds.FireballLobCooldownFrames);
            AssertSkillSlotStartsAbility(
                slotIndex: 6,
                CharacterTestAbilityIds.TornadoAbilityId,
                (CharacterTestCharacterActionConfigSource.TornadoActionId).ToString(),
                CharacterTestAbilityIds.TornadoCooldownFrames);
            AssertSkillSlotStartsAbility(
                slotIndex: 7,
                CharacterTestAbilityIds.FireballSwayAbilityId,
                (CharacterTestCharacterActionConfigSource.FireballSwayActionId).ToString(),
                CharacterTestAbilityIds.FireballSwayCooldownFrames);
            AssertSkillSlotStartsAbility(
                slotIndex: 8,
                CharacterTestAbilityIds.FireballHomingAbilityId,
                (CharacterTestCharacterActionConfigSource.FireballHomingActionId).ToString(),
                CharacterTestAbilityIds.FireballHomingCooldownFrames);
        }

[Test]
        public void CharacterActionSystem_LaserLanceAssignedHotbarSlotStartsAbility()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var hotbar = new CharacterTestAbilityHotbarRuntime(abilityRuntime.Definitions);
            Assert.IsTrue(hotbar.AssignSlot(9, CharacterTestAbilityIds.LaserLanceAbilityId, out string message), message);
            Assert.IsTrue(hotbar.TryGetAbilityForSlot(9, out AbilityDefinitionConfig ability));
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime);

            abilityRuntime.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PreSimulation));
            attacker.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.zero,
                jumpPressed: false,
                abilitySlotPressed: 9,
                abilityIdPressed: ability.AbilityId));
            QueueGameplayIntent(
                attacker,
                CharacterTestGameplayIntentKind.AbilitySlotCast,
                RuntimeFrame.Zero,
                abilitySlotPressed: 9,
                abilityIdPressed: ability.AbilityId);

            system.Tick(world, RuntimeFrame.Zero);

            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LaserLanceActionId, attacker.Action.ActiveActionId);
            Assert.AreEqual(
                CharacterTestAbilityIds.LaserLanceCooldownFrames,
                abilityRuntime.GetRemainingCooldownFrames(attacker.Id.Value, ability, RuntimeFrame.Zero));
        }

[Test]
        public void CharacterActionSystem_ChannelBeamAssignedHotbarSlotStartsAbility()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var hotbar = new CharacterTestAbilityHotbarRuntime(abilityRuntime.Definitions);
            Assert.IsTrue(hotbar.AssignSlot(9, CharacterTestAbilityIds.ChannelBeamAbilityId, out string message), message);
            Assert.IsTrue(hotbar.TryGetAbilityForSlot(9, out AbilityDefinitionConfig ability));
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime);

            abilityRuntime.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PreSimulation));
            attacker.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.zero,
                jumpPressed: false,
                abilitySlotPressed: 9,
                abilityIdPressed: ability.AbilityId));
            QueueGameplayIntent(
                attacker,
                CharacterTestGameplayIntentKind.AbilitySlotCast,
                RuntimeFrame.Zero,
                abilitySlotPressed: 9,
                abilityIdPressed: ability.AbilityId);

            system.Tick(world, RuntimeFrame.Zero);

            Assert.AreEqual(CharacterTestCharacterActionConfigSource.ChannelBeamActionId, attacker.Action.ActiveActionId);
            Assert.AreEqual(
                CharacterTestAbilityIds.ChannelBeamCooldownFrames,
                abilityRuntime.GetRemainingCooldownFrames(attacker.Id.Value, ability, RuntimeFrame.Zero));
        }

[Test]
        public void CharacterTestNetworkProjectileSnapshot_RestoresFlameLancePierceHitHistory()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            AlignTargetsWithDefaultProjectileSocketLine(firstTarget, secondTarget);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.FlameLanceIntentId,
                RuntimeFrame.Zero));
            RunUntilCombatResults(
                world,
                runtime,
                motion,
                combat,
                attacker,
                expectedAcceptedDamageCount: 2,
                maxFrames: 20,
                startFrame: 0);
            CombatProjectileRuntimeState originalState = combat.CreateProjectileStateSnapshot();
            Assert.AreEqual(2, originalState.ActiveProjectiles[0].AcceptedImpactCount);

            CharacterTestNetworkProjectileRuntimeSnapshot networkSnapshot =
                CharacterTestNetworkProjectileSnapshotCodec.Capture(combat);
            Assert.AreEqual(2, networkSnapshot.ActiveProjectiles[0].AcceptedImpacts.Length);
            Assert.AreEqual(
                originalState.ActiveProjectiles[0].EffectiveLifetimeFrames,
                networkSnapshot.ActiveProjectiles[0].EffectiveLifetimeFrames);

            var restoredCombat = new CharacterTestCharacterCombatTraceSystem();
            restoredCombat.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
            NetworkSnapshotApplyResult applyResult =
                CharacterTestNetworkProjectileSnapshotCodec.Apply(restoredCombat, networkSnapshot);

            Assert.IsTrue(applyResult.Success, applyResult.Message);
            CombatProjectileRuntimeState restoredState = restoredCombat.CreateProjectileStateSnapshot();
            Assert.AreEqual(2, restoredState.ActiveProjectiles[0].AcceptedImpactCount);
            Assert.AreEqual(
                originalState.ActiveProjectiles[0].EffectiveLifetimeFrames,
                restoredState.ActiveProjectiles[0].EffectiveLifetimeFrames);
            RuntimeHashAccumulator originalHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkProjectileSnapshotCodec.AddStateHash(originalHash, originalState);
            RuntimeHashAccumulator restoredHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkProjectileSnapshotCodec.AddStateHash(restoredHash, restoredState);
            Assert.AreEqual(originalHash.ToHash(), restoredHash.ToHash());
            RuntimeHashAccumulator ownedHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkProjectileSnapshotCodec.AddStateHash(
                ownedHash,
                originalState,
                originalState.ActiveProjectiles[0].SourceEntityId.Value);
            Assert.AreNotEqual(originalHash.ToHash(), ownedHash.ToHash());
            RuntimeHashAccumulator nonOwnedHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkProjectileSnapshotCodec.AddStateHashExcludingSource(
                nonOwnedHash,
                originalState,
                originalState.ActiveProjectiles[0].SourceEntityId.Value);
            Assert.AreNotEqual(originalHash.ToHash(), nonOwnedHash.ToHash());
            Assert.AreNotEqual(ownedHash.ToHash(), nonOwnedHash.ToHash());
            CharacterTestNetworkProjectileRuntimeSnapshot changedNetworkSnapshot =
                networkSnapshot.CloneForRollback();
            changedNetworkSnapshot.ActiveProjectiles[0].DefinitionId += 1;
            changedNetworkSnapshot.ActiveProjectiles[0].TraceId += 1;
            RuntimeHashAccumulator changedFieldHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkProjectileSnapshotCodec.AddStateHash(
                changedFieldHash,
                changedNetworkSnapshot.ToState());
            Assert.AreNotEqual(originalHash.ToHash(), changedFieldHash.ToHash());
        }

[Test]
        public void CharacterTestNetworkProjectileSnapshot_RestoresHomingTrackingTarget()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(4f, 0f, 8f, 0f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetLockedTargetEntityIdProvider(() => target.Id.Value);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.FireballHomingIntentId,
                RuntimeFrame.Zero));
            CombatProjectileSnapshot originalProjectile =
                RunUntilProjectileSpawn(world, runtime, combat, maxFrames: 10);
            CombatProjectileRuntimeState originalState = combat.CreateProjectileStateSnapshot();
            Assert.IsTrue(originalProjectile.HasTrackingTarget);

            CharacterTestNetworkProjectileRuntimeSnapshot networkSnapshot =
                CharacterTestNetworkProjectileSnapshotCodec.Capture(combat);
            Assert.IsTrue(networkSnapshot.ActiveProjectiles[0].HasTrackingTarget);
            Assert.AreEqual(
                originalProjectile.TrackingTargetEntityId.Value,
                networkSnapshot.ActiveProjectiles[0].TrackingTargetEntityId);

            var restoredCombat = new CharacterTestCharacterCombatTraceSystem();
            restoredCombat.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
            NetworkSnapshotApplyResult applyResult =
                CharacterTestNetworkProjectileSnapshotCodec.Apply(restoredCombat, networkSnapshot);

            Assert.IsTrue(applyResult.Success, applyResult.Message);
            CombatProjectileRuntimeState restoredState = restoredCombat.CreateProjectileStateSnapshot();
            CombatProjectileSnapshot restoredProjectile = restoredState.ActiveProjectiles[0];
            Assert.IsTrue(restoredProjectile.HasTrackingTarget);
            Assert.AreEqual(originalProjectile.TrackingTargetEntityId, restoredProjectile.TrackingTargetEntityId);
            RuntimeHashAccumulator originalHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkProjectileSnapshotCodec.AddStateHash(originalHash, originalState);
            RuntimeHashAccumulator restoredHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkProjectileSnapshotCodec.AddStateHash(restoredHash, restoredState);
            Assert.AreEqual(originalHash.ToHash(), restoredHash.ToHash());
            RuntimeHashAccumulator ownedHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkProjectileSnapshotCodec.AddStateHash(
                ownedHash,
                originalState,
                originalState.ActiveProjectiles[0].SourceEntityId.Value);
            Assert.AreNotEqual(originalHash.ToHash(), ownedHash.ToHash());
            RuntimeHashAccumulator nonOwnedHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkProjectileSnapshotCodec.AddStateHashExcludingSource(
                nonOwnedHash,
                originalState,
                originalState.ActiveProjectiles[0].SourceEntityId.Value);
            Assert.AreNotEqual(originalHash.ToHash(), nonOwnedHash.ToHash());
            Assert.AreNotEqual(ownedHash.ToHash(), nonOwnedHash.ToHash());
            CharacterTestNetworkProjectileRuntimeSnapshot changedNetworkSnapshot =
                networkSnapshot.CloneForRollback();
            changedNetworkSnapshot.ActiveProjectiles[0].DefinitionId += 1;
            changedNetworkSnapshot.ActiveProjectiles[0].TraceId += 1;
            RuntimeHashAccumulator changedFieldHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkProjectileSnapshotCodec.AddStateHash(
                changedFieldHash,
                changedNetworkSnapshot.ToState());
            Assert.AreNotEqual(originalHash.ToHash(), changedFieldHash.ToHash());
        }

[Test]
        public void CharacterActionRuntime_FireballProjectile_CanDamageAgainAfterActionCompletes()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AlignTargetWithDefaultProjectileSocketLine(target);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime);

            StartFireballThroughActionSystem(
                system,
                world,
                attacker,
                RuntimeFrame.Zero);
            HitResolveResult firstResult = RunUntilCombatResult(
                world,
                system.Runtime,
                motion,
                combat,
                attacker,
                maxFrames: 20,
                startFrame: 0);
            Assert.AreEqual(HitResolveKind.Damage, firstResult.Kind);
            int healthAfterFirst = target.CombatState.Health;

            TickCombatUntilActionInactive(
                world,
                system.Runtime,
                motion,
                combat,
                attacker,
                startFrame: 20,
                maxFrames: 80);

            StartFireballThroughActionSystem(
                system,
                world,
                attacker,
                new RuntimeFrame(120));
            HitResolveResult secondResult = RunUntilCombatResult(
                world,
                system.Runtime,
                motion,
                combat,
                attacker,
                maxFrames: 20,
                startFrame: 120);

            Assert.AreEqual(HitResolveKind.Damage, secondResult.Kind);
            Assert.AreEqual(firstResult.Damage, secondResult.Damage);
            Assert.AreEqual(healthAfterFirst - secondResult.Damage, target.CombatState.Health);
            Assert.AreEqual(target.CombatState.Health, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
        }

[Test]
        public void CharacterActionRuntime_FireballExplosionProjectile_StartsDelayedAfterblastTimeline()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AlignTargetWithDefaultProjectileSocketLine(target);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetAbilityDefinitions(CharacterTestAbilityCatalogSource.CreateRegistry());
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            combat.SetVfxRuntime(vfx);
            var audioService = new RecordingAudioService();
            var audioPlayback = new CharacterTestAudioCuePlaybackSystem(
                audioService,
                CharacterTestAudioCueCatalog.CreateDefault(),
                NullRuntimeLogger.Instance);
            var interactionVfx = new CharacterTestInteractionVfxSystem(vfx);
            var interactionAudio = new CharacterTestInteractionAudioSystem(audioPlayback);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.FireballExplosionIntentId,
                RuntimeFrame.Zero));

            int acceptedDamageFrames = 0;
            int timelineStartCount = 0;
            int timelineVfxCount = 0;
            int timelineCombatCount = 0;
            int timelineSuppressedCombatCount = 0;
            int interactionVfxCount = 0;
            int interactionAudioCount = 0;
            int firstDamage = 0;
            int secondDamage = 0;
            int healthAfterFirst = target.CombatState.Health;
            bool projectileGoneBeforeSecondDamage = false;
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            for (int i = 0; i < 180; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
                interactionVfx.Tick(combat.LastFrameInteractionEvents, frame);
                interactionAudio.Tick(combat.LastFrameInteractionEvents, frame);
                timelineStartCount += combat.LastAbilityEffectTimelineStartCount;
                timelineVfxCount += combat.LastAbilityEffectTimelineVfxSpawnCount;
                timelineCombatCount += combat.LastAbilityEffectTimelineCombatEventCount;
                timelineSuppressedCombatCount += combat.LastAbilityEffectTimelineSuppressedCombatEventCount;
                interactionVfxCount += interactionVfx.LastSummary.VfxSpawnedCount;
                interactionAudioCount += interactionAudio.LastSummary.AudioPlayedCount;

                CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                bool projectileActive = projectileState != null && projectileState.ActiveCount > 0;
                if (!projectileActive && acceptedDamageFrames == 1)
                    projectileGoneBeforeSecondDamage = true;

                int frameDamage = attacker.LastCombatHitResults
                    .Where(result => result.IsAcceptedDamage)
                    .Sum(result => result.Damage);
                if (frameDamage <= 0)
                    continue;

                acceptedDamageFrames++;
                if (acceptedDamageFrames == 1)
                {
                    firstDamage = frameDamage;
                    healthAfterFirst = target.CombatState.Health;
                }
                else if (acceptedDamageFrames == 2)
                {
                    secondDamage = frameDamage;
                    break;
                }
            }

            Assert.AreEqual(1, timelineStartCount, "Projectile impact should start exactly one afterblast timeline.");
            Assert.AreEqual(2, acceptedDamageFrames, "Expected first AreaPulseOnImpact damage plus delayed timeline damage.");
            Assert.Greater(firstDamage, 0);
            Assert.Greater(secondDamage, 0);
            Assert.AreEqual(
                CharacterTestCharacterCombatStateComponent.DefaultMaxHealth - firstDamage - secondDamage,
                target.CombatState.Health);
            Assert.Less(healthAfterFirst, CharacterTestCharacterCombatStateComponent.DefaultMaxHealth);
            Assert.IsTrue(projectileGoneBeforeSecondDamage, "Timeline damage must continue after the projectile is consumed.");
            Assert.AreEqual(0, timelineSuppressedCombatCount, "This afterblast timeline has no local frame 0 combat event.");
            Assert.AreEqual(1, timelineCombatCount, "Only the delayed frame 120 combat event should resolve.");
            Assert.AreEqual(0, timelineVfxCount, "Fireball explosion VFX should be driven by interaction VFX consumer.");
            Assert.AreEqual(2, interactionVfxCount, "Initial and delayed explosion interactions should each spawn VFX.");
            Assert.AreEqual(2, interactionAudioCount, "Initial and delayed explosion interactions should each play audio.");
            Assert.AreEqual(2, audioService.PlayOneShotCount);
            Assert.AreEqual(2, vfx.PendingSpawnCount);
        }

[Test]
        public void CharacterActionSystem_FireballCooldownReject_DoesNotApplyDamageOrPublishPresentationEvent()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AlignTargetWithDefaultProjectileSocketLine(target);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            Assert.IsTrue(abilityRuntime.Definitions.TryGet(
                CharacterTestAbilityIds.FireballAbilityId,
                out AbilityDefinitionConfig fireball));
            var trace = new CharacterTestTraceRecorder(capacity: 16);
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime);
            system.SetTraceRecorder(trace);
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            var actionVfxBridge = new CharacterTestActionVfxBridgeModule(
                () => world.Entities,
                vfx,
                NullRuntimeLogger.Instance);

            abilityRuntime.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PreSimulation));
            QueueGameplayIntent(
                attacker,
                CharacterTestGameplayIntentKind.AttackSecondary,
                RuntimeFrame.Zero);
            system.Tick(world, RuntimeFrame.Zero);
            attacker.SetInput(CharacterTestCharacterInputComponent.None);
            actionVfxBridge.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(1, actionVfxBridge.LastFrameRequestCount);
            Assert.AreEqual(1, actionVfxBridge.LastFrameSpawnedCount);
            Assert.AreEqual(1, vfx.PendingSpawnCount);
            HitResolveResult firstResult = RunUntilCombatResult(
                world,
                system.Runtime,
                motion,
                combat,
                attacker,
                maxFrames: 20,
                startFrame: 0);
            Assert.AreEqual(
                HitResolveKind.Damage,
                firstResult.Kind,
                "Initial Fireball cast should resolve damage before the cooldown reject check.");
            int healthAfterFirst = target.CombatState.Health;
            int presentationCountAfterFirst = combat.HitPresentationEvents.Count;
            Assert.AreEqual(1, presentationCountAfterFirst);

            int presentationCountBeforeCooldownReject = combat.HitPresentationEvents.Count;

            abilityRuntime.Tick(new RuntimeTickContext(30, 0d, 0d, RuntimeTickStage.PreSimulation));
            QueueGameplayIntent(
                attacker,
                CharacterTestGameplayIntentKind.AttackSecondary,
                new RuntimeFrame(30));
            system.Tick(world, new RuntimeFrame(30));
            attacker.SetInput(CharacterTestCharacterInputComponent.None);
            actionVfxBridge.Tick(new RuntimeTickContext(30, 0d, 0d, RuntimeTickStage.PostSimulation));

            Assert.AreEqual("OnCooldown", attacker.Action.LastRejectReason);
            Assert.AreEqual(30L, abilityRuntime.GetRemainingCooldownFrames(attacker.Id.Value, fireball, new RuntimeFrame(30)));
            Assert.AreEqual(healthAfterFirst, target.CombatState.Health);
            Assert.AreEqual(presentationCountBeforeCooldownReject, combat.HitPresentationEvents.Count);
            Assert.AreEqual(0, actionVfxBridge.LastFrameRequestCount);
            Assert.AreEqual(0, actionVfxBridge.LastFrameSpawnedCount);
            Assert.AreEqual(1, vfx.PendingSpawnCount);
            Assert.IsTrue(trace.RecentEvents.Any(evt =>
                evt.Warning
                && evt.Message.Contains("OnCooldown")
                && evt.Message.Contains("remainingCooldownFrames=30")));

            TickCombatUntilActionInactive(
                world,
                system.Runtime,
                motion,
                combat,
                attacker,
                startFrame: 31,
                maxFrames: 80);

            abilityRuntime.Tick(new RuntimeTickContext(120, 0d, 0d, RuntimeTickStage.PreSimulation));
            QueueGameplayIntent(
                attacker,
                CharacterTestGameplayIntentKind.AttackSecondary,
                new RuntimeFrame(120));
            system.Tick(world, new RuntimeFrame(120));
            attacker.SetInput(CharacterTestCharacterInputComponent.None);
            HitResolveResult secondResult = RunUntilCombatResult(
                world,
                system.Runtime,
                motion,
                combat,
                attacker,
                maxFrames: 20,
                startFrame: 120);

            Assert.AreEqual(
                HitResolveKind.Damage,
                secondResult.Kind,
                "Fireball should cast again after the 60 frame cooldown expires.");
            Assert.AreEqual(firstResult.Damage, secondResult.Damage);
            Assert.AreEqual(healthAfterFirst - secondResult.Damage, target.CombatState.Health);
        }

[Test]
        public void CharacterTestMechanicSources_DefineAreaConfinementAsStableSourceAndMotionPolicy()
        {
            var areaConfinement = new CharacterTestAreaConfinementRuntime();
            CharacterTestMechanicSourceDefinitionRegistry sourceDefinitions = areaConfinement.Definitions;
            Assert.AreEqual(60, areaConfinement.Tuning.TelegraphFrames);
            Assert.AreEqual(CharacterTestAreaConfinementIds.ZoneDurationFrames, areaConfinement.Tuning.ZoneDurationFrames);
            Assert.AreEqual(CharacterTestAreaConfinementIds.RadiusCentimeters, areaConfinement.Tuning.ZoneRadiusCentimeters);
            Assert.AreEqual(
                CharacterTestAreaConfinementInterruptPolicy.CancelOnDamageOrActionExit,
                areaConfinement.Tuning.InterruptPolicy);

            Assert.AreEqual(1, sourceDefinitions.Count);
            Assert.IsTrue(sourceDefinitions.TryGet(
                CharacterTestAreaConfinementIds.MechanicSourceId,
                out CharacterTestMechanicSourceDefinition sourceDefinition));
            Assert.IsTrue(sourceDefinitions.TryGetByStableId(
                CharacterTestAreaConfinementIds.SourceStableId,
                out CharacterTestMechanicSourceDefinition sourceDefinitionByStableId));
            Assert.AreEqual(sourceDefinition, sourceDefinitionByStableId);
            Assert.AreEqual(CharacterTestMechanicSourceShape.Sphere, sourceDefinition.Shape);
            Assert.AreEqual(Fix64.FromRatio(CharacterTestAreaConfinementIds.RadiusCentimeters, 100), sourceDefinition.Radius);
            Assert.AreEqual(GameplayTargetRelationFilter.Enemy, sourceDefinition.RelationFilter);
            Assert.AreEqual(CharacterTestAreaConfinementIds.ZoneDurationFrames, sourceDefinition.DurationFrames);
            Assert.AreEqual(2, sourceDefinition.EffectCount);
            Assert.AreNotEqual(0, sourceDefinitions.ComputeStableHash());

            CharacterTestMechanicEffectDefinition buffEffect = sourceDefinition.GetEffect(0);
            Assert.AreEqual(CharacterTestMechanicEffectKind.ApplyBuffOnTick, buffEffect.Kind);
            Assert.AreEqual(CharacterTestAreaConfinementIds.RootedBuffId, buffEffect.BuffId);

            CharacterTestMechanicEffectDefinition vfxEffect = sourceDefinition.GetEffect(1);
            Assert.AreEqual(CharacterTestMechanicEffectKind.SpawnVfxOnCreate, vfxEffect.Kind);
            Assert.AreEqual(CharacterTestVfxIds.AreaConfinementZoneDefinitionId, vfxEffect.VfxDefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.AreaConfinementZoneLifetimeFrames, vfxEffect.VfxLifetimeFrames);
            Assert.Throws<ArgumentException>(() =>
                _ = new CharacterTestMechanicSourceDefinitionRegistry(sourceDefinition, sourceDefinition));

            CharacterTestMechanicMotionPolicyRegistry motionPolicies =
                CharacterTestMechanicMotionPolicySource.CreateDefaultRegistry();
            CharacterTestMechanicMotionPolicyDefinition[] policies = motionPolicies.ToArray();
            Assert.AreEqual(3, policies.Length);
            CharacterTestMechanicMotionPolicyDefinition areaPolicy =
                policies.Single(policy => policy.PolicyId == CharacterTestAreaConfinementIds.MotionPolicyId);
            Assert.AreEqual(CharacterTestAreaConfinementIds.MotionPolicyStableId, areaPolicy.StableId);
            Assert.AreEqual(
                new GameplayStatusId(CharacterTestAreaConfinementIds.AreaConfinedStatusId),
                areaPolicy.StatusId);
            Assert.AreEqual(CharacterTestMechanicMotionPolicyKind.RootActiveMovement, areaPolicy.Kind);
            Assert.AreEqual(CharacterMovementMode.ControlLocked, areaPolicy.MovementMode);
            Assert.AreEqual(0, areaPolicy.MovementControlScaleMilli);
            Assert.AreEqual(CharacterTestDeterministicUnits.ScaleFactorScale, areaPolicy.RotationControlScaleMilli);
            Assert.AreEqual(CharacterTestMechanicExternalMotionPolicy.Allow, areaPolicy.ExternalMotionPolicy);
            CharacterTestMechanicMotionPolicyDefinition stasisPolicy =
                policies.Single(policy => policy.PolicyId == CharacterTestStasisIds.StasisMotionPolicyId);
            Assert.AreEqual(CharacterTestStasisIds.StasisMotionPolicyStableId, stasisPolicy.StableId);
            Assert.AreEqual(
                new GameplayStatusId(CharacterTestStasisIds.StasisStatusId),
                stasisPolicy.StatusId);
            Assert.AreEqual(CharacterTestMechanicMotionPolicyKind.ControlScale, stasisPolicy.Kind);
            Assert.AreEqual(0, stasisPolicy.MovementControlScaleMilli);
            Assert.AreEqual(0, stasisPolicy.RotationControlScaleMilli);
            CharacterTestMechanicMotionPolicyDefinition stunPolicy =
                policies.Single(policy => policy.PolicyId == CharacterTestStunIds.StunMotionPolicyId);
            Assert.AreEqual(CharacterTestStunIds.StunMotionPolicyStableId, stunPolicy.StableId);
            Assert.AreEqual(
                new GameplayStatusId(CharacterTestStunIds.StunnedStatusId),
                stunPolicy.StatusId);
            Assert.AreEqual(CharacterTestMechanicMotionPolicyKind.ControlScale, stunPolicy.Kind);
            Assert.AreEqual(0, stunPolicy.MovementControlScaleMilli);
            Assert.AreEqual(0, stunPolicy.RotationControlScaleMilli);
            Assert.AreNotEqual(0, motionPolicies.ComputeStableHash());
            Assert.Throws<ArgumentException>(() =>
                _ = new CharacterTestMechanicMotionPolicyRegistry(areaPolicy, areaPolicy));
        }

[Test]
        public void CharacterTestVfxDefinitionSource_MapsFireballPresentationTriggerToPrefabResource()
        {
            VfxDefinitionRegistry definitions = CharacterTestVfxDefinitionSource.CreateDefinitions();
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.FireballDefinitionId),
                out VfxDefinition definition));
            Assert.AreEqual(CharacterTestVfxIds.FireballResourceKey, definition.PrefabResource);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.FireballChargeDefinitionId),
                out VfxDefinition fireballChargeDefinition));
            Assert.AreEqual(CharacterTestVfxIds.FireballChargeResourceKey, fireballChargeDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.FireballChargeLifetimeFrames,
                fireballChargeDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.GuardAuraDefinitionId),
                out VfxDefinition guardAuraDefinition));
            Assert.AreEqual(CharacterTestVfxIds.GuardAuraResourceKey, guardAuraDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.GuardAuraLifetimeFrames,
                guardAuraDefinition.DefaultLifetimeFrames);

            ResourceCatalog catalog = CharacterTestVfxDefinitionSource.CreateResourceCatalog();
            AssertEntry(
                catalog,
                CharacterTestVfxIds.GuardAuraResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "Aura");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.FireballExplosionImpactResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "FireballExplosion");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.BeamLanceBeamResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "BeamLance");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.LaserLanceImpactResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "LaserLance");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.LaserLanceChargeResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "LaserLance");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.ChannelBeamChargeResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "ChannelBeam");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.ChannelBeamBeamResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "ChannelBeam");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.ChannelBeamImpactResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "ChannelBeam");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.TornadoFlightResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "Tornado");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.TornadoImpactResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "Tornado");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.TornadoTickResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "Tornado");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.AreaConfinementZoneResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "AreaConfinement");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.AreaConfinementCharacterResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "AreaConfinement");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.AreaConfinementTelegraphResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "AreaConfinement");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.AreaConfinementTelegraphCancelResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "AreaConfinement");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.AreaConfinementCharacterReleaseResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "AreaConfinement");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.StunnedStatusResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "Status");
            AssertEntry(
                catalog,
                CharacterTestVfxIds.ParalyzedStatusResourceId,
                ResourceTypeIds.GameObject,
                MemoryResourceProvider.Id,
                "Status");

            VfxTriggerMapResolver resolver = CharacterTestVfxDefinitionSource.CreateTriggerResolver();
            int trigger = MxFramework.Vfx.CombatBridge.CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.FireballPresentationProfileId);
            var output = new List<VfxSpawnRequest>();
            int count = resolver.Resolve(new VfxTriggerContext(
                RuntimeFrame.Zero,
                VfxSourceDomains.CombatHit,
                trigger,
                sourceId: 1,
                targetId: 2,
                sourceFrame: 6,
                localFrame: 6,
                sourceOrder: 0,
                VfxAnchor.Target(1, 2, VfxPose.Identity),
                VfxParameterBlock.Empty,
                traceId: new RuntimeTraceIdentity(998374)), output);

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, output.Count);
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.FireballDefinitionId), output[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.FireballLifetimeFrames, output[0].LifetimeFramesOverride);

            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.FireballBurstImpactDefinitionId),
                out VfxDefinition burstDefinition));
            Assert.AreEqual(CharacterTestVfxIds.FireballBurstImpactResourceKey, burstDefinition.PrefabResource);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.FireballBurstChargeDefinitionId),
                out VfxDefinition burstChargeDefinition));
            Assert.AreEqual(CharacterTestVfxIds.FireballBurstChargeResourceKey, burstChargeDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.FireballBurstChargeLifetimeFrames,
                burstChargeDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.FireballExplosionImpactDefinitionId),
                out VfxDefinition explosionImpactDefinition));
            Assert.AreEqual(CharacterTestVfxIds.FireballExplosionImpactResourceKey, explosionImpactDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.FireballExplosionImpactLifetimeFrames,
                explosionImpactDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.TornadoChargeDefinitionId),
                out VfxDefinition tornadoChargeDefinition));
            Assert.AreEqual(CharacterTestVfxIds.TornadoChargeResourceKey, tornadoChargeDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.TornadoChargeLifetimeFrames,
                tornadoChargeDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.TornadoImpactDefinitionId),
                out VfxDefinition tornadoImpactDefinition));
            Assert.AreEqual(CharacterTestVfxIds.TornadoImpactResourceKey, tornadoImpactDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.TornadoImpactLifetimeFrames,
                tornadoImpactDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.TornadoTickDefinitionId),
                out VfxDefinition tornadoTickDefinition));
            Assert.AreEqual(CharacterTestVfxIds.TornadoTickResourceKey, tornadoTickDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.TornadoTickLifetimeFrames,
                tornadoTickDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.BeamLanceBeamDefinitionId),
                out VfxDefinition laserBeamDefinition));
            Assert.AreEqual(CharacterTestVfxIds.BeamLanceBeamResourceKey, laserBeamDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.BeamLanceBeamLifetimeFrames,
                laserBeamDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.LaserLanceImpactDefinitionId),
                out VfxDefinition laserImpactDefinition));
            Assert.AreEqual(CharacterTestVfxIds.LaserLanceImpactResourceKey, laserImpactDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.LaserLanceImpactLifetimeFrames,
                laserImpactDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.LaserLanceChargeDefinitionId),
                out VfxDefinition laserChargeDefinition));
            Assert.AreEqual(CharacterTestVfxIds.LaserLanceChargeResourceKey, laserChargeDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.LaserLanceChargeLifetimeFrames,
                laserChargeDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ChannelBeamChargeDefinitionId),
                out VfxDefinition channelBeamChargeDefinition));
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamChargeResourceKey, channelBeamChargeDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.ChannelBeamChargeLifetimeFrames,
                channelBeamChargeDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ChannelBeamBeamDefinitionId),
                out VfxDefinition channelBeamBeamDefinition));
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamBeamResourceKey, channelBeamBeamDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.ChannelBeamBeamLifetimeFrames,
                channelBeamBeamDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ChannelBeamImpactDefinitionId),
                out VfxDefinition channelBeamImpactDefinition));
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamImpactResourceKey, channelBeamImpactDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.ChannelBeamImpactLifetimeFrames,
                channelBeamImpactDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementZoneDefinitionId),
                out VfxDefinition areaConfinementZoneDefinition));
            Assert.AreEqual(CharacterTestVfxIds.AreaConfinementZoneResourceKey, areaConfinementZoneDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.AreaConfinementZoneLifetimeFrames,
                areaConfinementZoneDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementCharacterDefinitionId),
                out VfxDefinition areaConfinementCharacterDefinition));
            Assert.AreEqual(CharacterTestVfxIds.AreaConfinementCharacterResourceKey, areaConfinementCharacterDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.AreaConfinementCharacterLifetimeFrames,
                areaConfinementCharacterDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementTelegraphDefinitionId),
                out VfxDefinition areaConfinementTelegraphDefinition));
            Assert.AreEqual(CharacterTestVfxIds.AreaConfinementTelegraphResourceKey, areaConfinementTelegraphDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.AreaConfinementTelegraphLifetimeFrames,
                areaConfinementTelegraphDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementTelegraphCancelDefinitionId),
                out VfxDefinition areaConfinementTelegraphCancelDefinition));
            Assert.AreEqual(CharacterTestVfxIds.AreaConfinementTelegraphCancelResourceKey, areaConfinementTelegraphCancelDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.AreaConfinementTelegraphCancelLifetimeFrames,
                areaConfinementTelegraphCancelDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.AreaConfinementCharacterReleaseDefinitionId),
                out VfxDefinition areaConfinementCharacterReleaseDefinition));
            Assert.AreEqual(CharacterTestVfxIds.AreaConfinementCharacterReleaseResourceKey, areaConfinementCharacterReleaseDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.AreaConfinementCharacterReleaseLifetimeFrames,
                areaConfinementCharacterReleaseDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.StunnedStatusDefinitionId),
                out VfxDefinition stunnedStatusDefinition));
            Assert.AreEqual(CharacterTestVfxIds.StunnedStatusResourceKey, stunnedStatusDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.StunnedStatusLifetimeFrames,
                stunnedStatusDefinition.DefaultLifetimeFrames);
            Assert.IsTrue(definitions.TryGet(
                new VfxDefinitionId(CharacterTestVfxIds.ParalyzedStatusDefinitionId),
                out VfxDefinition paralyzedStatusDefinition));
            Assert.AreEqual(CharacterTestVfxIds.ParalyzedStatusResourceKey, paralyzedStatusDefinition.PrefabResource);
            Assert.AreEqual(
                CharacterTestVfxIds.ParalyzedStatusLifetimeFrames,
                paralyzedStatusDefinition.DefaultLifetimeFrames);
            int burstTrigger = MxFramework.Vfx.CombatBridge.CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.FireballBurstPresentationProfileId);
            output.Clear();
            int burstCount = resolver.Resolve(new VfxTriggerContext(
                RuntimeFrame.Zero,
                VfxSourceDomains.CombatHit,
                burstTrigger,
                sourceId: 1,
                targetId: 2,
                sourceFrame: 6,
                localFrame: 6,
                sourceOrder: 0,
                VfxAnchor.Target(1, 2, VfxPose.Identity),
                VfxParameterBlock.Empty,
                traceId: new RuntimeTraceIdentity(933127)), output);
            Assert.AreEqual(1, burstCount);
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.FireballBurstImpactDefinitionId), output[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.FireballBurstImpactLifetimeFrames, output[0].LifetimeFramesOverride);
            int tornadoTrigger = MxFramework.Vfx.CombatBridge.CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.TornadoPresentationProfileId);
            output.Clear();
            int tornadoCount = resolver.Resolve(new VfxTriggerContext(
                RuntimeFrame.Zero,
                VfxSourceDomains.CombatHit,
                tornadoTrigger,
                sourceId: 1,
                targetId: 2,
                sourceFrame: 6,
                localFrame: 6,
                sourceOrder: 0,
                VfxAnchor.Target(1, 2, VfxPose.Identity),
                VfxParameterBlock.Empty,
                traceId: new RuntimeTraceIdentity(914368)), output);
            Assert.AreEqual(1, tornadoCount);
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.TornadoImpactDefinitionId), output[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.TornadoImpactLifetimeFrames, output[0].LifetimeFramesOverride);
            int tornadoTickTrigger = MxFramework.Vfx.CombatBridge.CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.TornadoTickPresentationProfileId);
            output.Clear();
            int tornadoTickCount = resolver.Resolve(new VfxTriggerContext(
                RuntimeFrame.Zero,
                VfxSourceDomains.CombatHit,
                tornadoTickTrigger,
                sourceId: 1,
                targetId: 2,
                sourceFrame: 6,
                localFrame: 6,
                sourceOrder: 0,
                VfxAnchor.Target(1, 2, VfxPose.Identity),
                VfxParameterBlock.Empty,
                traceId: new RuntimeTraceIdentity(957281)), output);
            Assert.AreEqual(1, tornadoTickCount);
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.TornadoTickDefinitionId), output[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.TornadoTickLifetimeFrames, output[0].LifetimeFramesOverride);
            int laserTrigger = MxFramework.Vfx.CombatBridge.CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.LaserLancePresentationProfileId);
            output.Clear();
            int laserCount = resolver.Resolve(new VfxTriggerContext(
                RuntimeFrame.Zero,
                VfxSourceDomains.CombatHit,
                laserTrigger,
                sourceId: 1,
                targetId: 2,
                sourceFrame: 6,
                localFrame: 6,
                sourceOrder: 0,
                VfxAnchor.Target(1, 2, VfxPose.Identity),
                VfxParameterBlock.Empty,
                traceId: new RuntimeTraceIdentity(938760)), output);
            Assert.AreEqual(1, laserCount);
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.LaserLanceImpactDefinitionId), output[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.LaserLanceImpactLifetimeFrames, output[0].LifetimeFramesOverride);
            int channelBeamTrigger = MxFramework.Vfx.CombatBridge.CombatVfxBridge.GetHitPresentationTriggerId(
                CharacterTestCharacterActionConfigSource.ChannelBeamPresentationProfileId);
            output.Clear();
            int channelBeamCount = resolver.Resolve(new VfxTriggerContext(
                RuntimeFrame.Zero,
                VfxSourceDomains.CombatHit,
                channelBeamTrigger,
                sourceId: 1,
                targetId: 2,
                sourceFrame: 30,
                localFrame: 30,
                sourceOrder: 0,
                VfxAnchor.Target(1, 2, VfxPose.Identity),
                VfxParameterBlock.Empty,
                traceId: new RuntimeTraceIdentity(969393)), output);
            Assert.AreEqual(1, channelBeamCount);
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.ChannelBeamImpactDefinitionId), output[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamImpactLifetimeFrames, output[0].LifetimeFramesOverride);

            CombatProjectileUnityPresentationRegistry presentationRegistry =
                CharacterTestProjectilePresentationSource.CreateRegistry();
            Assert.IsTrue(presentationRegistry.TryGetV2(
                CharacterTestFireballProjectileAdapter.DefinitionId,
                out CombatProjectileUnityPresentationBindingV2 binding));
            Assert.AreEqual(CharacterTestVfxIds.FireballResourceKey, binding.FlightBodyResource);
            Assert.AreEqual(
                CharacterTestProjectilePresentationSource.FireballViewNamePrefix,
                binding.ViewNamePrefix);
            Assert.IsTrue(presentationRegistry.TryGetV2(
                CharacterTestFireballProjectileAdapter.BurstDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 burstBinding));
            Assert.AreEqual(CharacterTestVfxIds.FireballBurstFlightResourceKey, burstBinding.FlightBodyResource);
            Assert.AreEqual(
                CharacterTestProjectilePresentationSource.FireballViewNamePrefix,
                burstBinding.ViewNamePrefix);
            Assert.IsTrue(presentationRegistry.TryGetV2(
                CharacterTestFireballProjectileAdapter.FireballExplosionDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 explosionBinding));
            Assert.AreEqual(CharacterTestVfxIds.FireballBurstFlightResourceKey, explosionBinding.FlightBodyResource);
            Assert.AreEqual(
                CharacterTestProjectilePresentationSource.FireballViewNamePrefix,
                explosionBinding.ViewNamePrefix);
            Assert.IsTrue(presentationRegistry.TryGetV2(
                CharacterTestFireballProjectileAdapter.TornadoDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 tornadoBinding));
            Assert.AreEqual(CharacterTestVfxIds.TornadoFlightResourceKey, tornadoBinding.FlightBodyResource);
            Assert.AreEqual(
                CharacterTestProjectilePresentationSource.FireballViewNamePrefix,
                tornadoBinding.ViewNamePrefix);
            Assert.IsTrue(presentationRegistry.TryGetV2(
                CharacterTestFireballProjectileAdapter.BeamLanceDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 beamLanceBinding));
            Assert.AreEqual(CharacterTestVfxIds.BeamLanceBeamResourceKey, beamLanceBinding.FlightBodyResource);
            Assert.AreEqual(
                CharacterTestProjectilePresentationSource.FireballViewNamePrefix,
                beamLanceBinding.ViewNamePrefix);
            Assert.IsTrue(presentationRegistry.TryGetV2(
                CharacterTestFireballProjectileAdapter.LaserLanceDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 laserBinding));
            Assert.AreEqual(CharacterTestVfxIds.LaserLancePathResourceKey, laserBinding.FlightBodyResource);
            Assert.AreEqual(
                CharacterTestProjectilePresentationSource.FireballViewNamePrefix,
                laserBinding.ViewNamePrefix);
            Assert.IsTrue(presentationRegistry.TryGetV2(
                CharacterTestFireballProjectileAdapter.ChannelBeamDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 channelBeamBinding));
            Assert.AreEqual(CharacterTestVfxIds.ChannelBeamBeamResourceKey, channelBeamBinding.FlightBodyResource);
            Assert.AreEqual(
                CharacterTestProjectilePresentationSource.FireballViewNamePrefix,
                channelBeamBinding.ViewNamePrefix);
            Assert.IsTrue(presentationRegistry.TryGetV2(
                CharacterTestFireballProjectileAdapter.HookChainDefinitionId,
                out CombatProjectileUnityPresentationBindingV2 hookChainBinding));
            Assert.AreEqual(CharacterTestVfxIds.StunBoltFlightResourceKey, hookChainBinding.FlightBodyResource);
            Assert.AreEqual(
                CharacterTestProjectilePresentationSource.FireballViewNamePrefix,
                hookChainBinding.ViewNamePrefix);
        }

[Test]
        public void CharacterTestVfxResourceCatalog_ControlStatusKeysDoNotCollideWithSampleStatusAuras()
        {
            var resourceManager = new ResourceManager();
            resourceManager.RegisterProvider(new MemoryResourceProvider());
            resourceManager.AddCatalog(new ResourceCatalog(
                "test.sample.status_aura",
                CharacterTestVfxIds.FireballResourcePackageId,
                new[]
                {
                    new ResourceCatalogEntry(
                        "vfx.status_aura.stun",
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        "mxframework.samples/vfx/status_aura/stun",
                        packageId: CharacterTestVfxIds.FireballResourcePackageId),
                    new ResourceCatalogEntry(
                        "vfx.status_aura.lightning",
                        ResourceTypeIds.GameObject,
                        MemoryResourceProvider.Id,
                        "mxframework.samples/vfx/status_aura/lightning",
                        packageId: CharacterTestVfxIds.FireballResourcePackageId)
                }));

            Assert.DoesNotThrow(() =>
                resourceManager.AddCatalog(CharacterTestVfxDefinitionSource.CreateResourceCatalog()));
            Assert.AreNotEqual("vfx.status_aura.stun", CharacterTestVfxIds.StunnedStatusResourceId);
            Assert.AreNotEqual("vfx.status_aura.lightning", CharacterTestVfxIds.ParalyzedStatusResourceId);
        }

[Test]
        public void CharacterTestAuraVfxView_CreateRuntimePrefab_BuildsVisibleAuraLines()
        {
            GameObject prefab = CharacterTestAuraVfxView.CreateRuntimePrefab();
            GameObject instance = null;
            try
            {
                Assert.IsFalse(prefab.activeSelf);
                Assert.IsNotNull(prefab.GetComponent<CharacterTestAuraVfxView>());

                instance = UnityEngine.Object.Instantiate(prefab);
                instance.SetActive(true);

                LineRenderer[] lines = instance.GetComponentsInChildren<LineRenderer>(includeInactive: true);
                Assert.GreaterOrEqual(lines.Length, 19);
                Assert.That(lines, Has.Some.Matches<LineRenderer>(
                    line => line.enabled && line.positionCount >= 64 && line.startWidth > 0f));
                Assert.That(lines, Has.Some.Matches<LineRenderer>(
                    line => line.enabled && line.positionCount == 2 && line.startWidth > 0f));
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

[Test]
        public void CharacterTestAreaConfinementCharacterVfxView_CreateRuntimePrefab_BuildsVisibleBindingLines()
        {
            GameObject prefab = CharacterTestAreaConfinementCharacterVfxView.CreateRuntimePrefab();
            GameObject releasePrefab = null;
            GameObject instance = null;
            try
            {
                Assert.IsFalse(prefab.activeSelf);
                Assert.IsNotNull(prefab.GetComponent<CharacterTestAreaConfinementCharacterVfxView>());
                releasePrefab = CharacterTestAreaConfinementCharacterVfxView.CreateRuntimePrefab(
                    CharacterTestAreaConfinementCharacterVfxMode.Release);
                Assert.IsFalse(releasePrefab.activeSelf);
                Assert.IsNotNull(releasePrefab.GetComponent<CharacterTestAreaConfinementCharacterVfxView>());

                instance = UnityEngine.Object.Instantiate(prefab);
                instance.SetActive(true);

                LineRenderer[] lines = instance.GetComponentsInChildren<LineRenderer>(includeInactive: true);
                Assert.GreaterOrEqual(lines.Length, 7);
                Assert.That(lines, Has.Some.Matches<LineRenderer>(
                    line => line.enabled && line.loop && line.positionCount >= 64 && line.startWidth > 0f));
                Assert.That(lines, Has.Some.Matches<LineRenderer>(
                    line => line.enabled && !line.loop && line.positionCount == 3 && line.startWidth > 0f));
                Transform ringRoot = instance.transform.Find("ControlStatusRings");
                Transform sparkRoot = instance.transform.Find("ControlStatusSparks");
                Assert.IsNotNull(ringRoot);
                Assert.IsNotNull(sparkRoot);
                Assert.That(ringRoot.localPosition.y, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(sparkRoot.localPosition.y, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
                if (releasePrefab != null)
                    UnityEngine.Object.DestroyImmediate(releasePrefab);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

[Test]
        public void CharacterTestControlStatusVfxView_CreateRuntimePrefab_BuildsStunAndParalysisLoops()
        {
            GameObject stunPrefab = CharacterTestControlStatusVfxView.CreateRuntimePrefab();
            GameObject paralysisPrefab = CharacterTestControlStatusVfxView.CreateRuntimePrefab(
                CharacterTestControlStatusVfxMode.Paralyzed);
            GameObject instance = null;
            try
            {
                Assert.IsFalse(stunPrefab.activeSelf);
                Assert.IsFalse(paralysisPrefab.activeSelf);
                Assert.IsNotNull(stunPrefab.GetComponent<CharacterTestControlStatusVfxView>());
                Assert.IsNotNull(paralysisPrefab.GetComponent<CharacterTestControlStatusVfxView>());

                instance = UnityEngine.Object.Instantiate(stunPrefab);
                instance.SetActive(true);

                LineRenderer[] lines = instance.GetComponentsInChildren<LineRenderer>(includeInactive: true);
                Assert.GreaterOrEqual(lines.Length, 10);
                Assert.That(lines, Has.Some.Matches<LineRenderer>(
                    line => line.enabled && line.loop && line.positionCount >= 64 && line.startWidth > 0f));
                Assert.That(lines, Has.Some.Matches<LineRenderer>(
                    line => line.enabled && !line.loop && line.positionCount == 3 && line.startWidth > 0f));
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(paralysisPrefab);
                UnityEngine.Object.DestroyImmediate(stunPrefab);
            }
        }

[Test]
        public void CharacterTestStatusVfxBridge_ArbitratesPrimaryLoopVfxAndAvoidsDuplicateSpawns()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            var bridge = new CharacterTestStatusVfxBridgeModule(() => world, vfx);
            var pendingSpawns = new List<VfxSpawnRequest>();
            var pendingStops = new List<VfxStopRequest>();

            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStasisIds.StasisStatusId)));
            bridge.Tick(new RuntimeTickContext(1, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(0, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(0, vfx.CopyPendingSpawnRequests(pendingSpawns));

            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(
                    new GameplayStatusId(CharacterTestStasisIds.StasisStatusId),
                    new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));
            bridge.Tick(new RuntimeTickContext(2, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(1, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pendingSpawns));
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.StunnedStatusDefinitionId), pendingSpawns[0].DefinitionId);
            Assert.AreEqual(CharacterTestStatusVfxBridgeModule.StunnedStatusTraceId, pendingSpawns[0].TraceId);
            Assert.AreEqual(CharacterTestVfxAnchorResolver.ControlStatusHeadSocketId, pendingSpawns[0].Anchor.SocketId);

            vfx.Tick(new RuntimeTickContext(2, 0d, 0d, RuntimeTickStage.PostSimulation));
            pendingSpawns.Clear();
            bridge.Tick(new RuntimeTickContext(3, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(0, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(0, vfx.CopyPendingSpawnRequests(pendingSpawns));

            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(
                    new GameplayStatusId(CharacterTestStunIds.StunnedStatusId),
                    new GameplayStatusId(CharacterTestParalysisIds.ParalyzedStatusId)));
            bridge.Tick(new RuntimeTickContext(4, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(1, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(1, bridge.LastFrameStoppedCount);
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pendingSpawns));
            Assert.AreEqual(1, vfx.CopyPendingStopRequests(pendingStops));
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.ParalyzedStatusDefinitionId), pendingSpawns[0].DefinitionId);
            Assert.AreEqual(CharacterTestStatusVfxBridgeModule.ParalyzedStatusTraceId, pendingSpawns[0].TraceId);
            Assert.AreEqual(CharacterTestVfxAnchorResolver.ControlStatusHeadSocketId, pendingSpawns[0].Anchor.SocketId);

            vfx.Tick(new RuntimeTickContext(4, 0d, 0d, RuntimeTickStage.PostSimulation));
            pendingSpawns.Clear();
            pendingStops.Clear();
            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));
            bridge.Tick(new RuntimeTickContext(5, 0d, 0d, RuntimeTickStage.PostSimulation));
            Assert.AreEqual(1, bridge.LastFrameSpawnedCount);
            Assert.AreEqual(1, bridge.LastFrameStoppedCount);
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pendingSpawns));
            Assert.AreEqual(1, vfx.CopyPendingStopRequests(pendingStops));
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.StunnedStatusDefinitionId), pendingSpawns[0].DefinitionId);
            Assert.AreEqual(CharacterTestVfxAnchorResolver.ControlStatusHeadSocketId, pendingSpawns[0].Anchor.SocketId);
        }

[Test]
        public void CharacterSpawnSpec_TemporaryAiOpponents_AreProjectileVerificationTargets()
        {
            CharacterTestCharacterSpawnSpec opponent = CharacterTestCharacterSpawnSpec.TemporaryAiOpponent;
            CharacterTestCharacterSpawnSpec nearby = CharacterTestCharacterSpawnSpec.TemporaryAiOpponentNearby;

            Assert.AreEqual(2, opponent.TeamId);
            Assert.AreEqual(CharacterTestCharacterControlOwner.None, opponent.ControlOwner);
            Assert.AreEqual(-0.25f, opponent.X, 0.001f);
            Assert.AreEqual(4f, opponent.Z, 0.001f);
            Assert.AreEqual(2, nearby.TeamId);
            Assert.AreEqual(CharacterTestCharacterControlOwner.None, nearby.ControlOwner);
            Assert.AreEqual(1.25f, nearby.X, 0.001f);
            Assert.AreEqual(4f, nearby.Z, 0.001f);
            Assert.Less(Math.Abs(nearby.X - opponent.X), 2f);
            Assert.AreEqual(opponent.Z, nearby.Z, 0.001f);
        }

[Test]
        public void CharacterRuntimeView_VfxSocketAnchorResolutionIsCached()
        {
            var go = new GameObject("character-test-view-socket-anchor-cache");
            var socket = new GameObject("Socket 7").transform;
            CharacterTestRuntimeCharacterView view = null;
            try
            {
                socket.SetParent(go.transform, worldPositionStays: false);
                socket.position = new Vector3(2f, 3f, 4f);
                CharacterBoneRuntimeSync boneSync = go.AddComponent<CharacterBoneRuntimeSync>();
                ConfigureBoneRuntimeSyncBinding(boneSync, "socket:7", socket);
                view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    go,
                    null,
                    null,
                    null,
                    boneSync);

                Assert.IsTrue(view.TryResolveVfxAnchor(7, out Vector3 firstPosition, out Vector3 firstForward));
                socket.position = new Vector3(5f, 6f, 7f);
                Assert.IsTrue(view.TryResolveVfxAnchor(7, out Vector3 secondPosition, out Vector3 secondForward));

                Assert.AreEqual(new Vector3(2f, 3f, 4f), firstPosition);
                Assert.AreEqual(new Vector3(5f, 6f, 7f), secondPosition);
                Assert.AreEqual(Vector3.forward, firstForward);
                Assert.AreEqual(Vector3.forward, secondForward);
                Assert.AreEqual(1, view.SocketAnchorResolveCount);
            }
            finally
            {
                view?.Release();
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
        }

[Test]
        public void CharacterRuntimeView_ControlStatusVfxAnchorUsesHeadTop()
        {
            var go = new GameObject("character-test-view-control-status-head-anchor");
            var head = new GameObject("Head").transform;
            CharacterTestRuntimeCharacterView view = null;
            try
            {
                go.transform.position = new Vector3(2f, 0f, 3f);
                go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                head.SetParent(go.transform, worldPositionStays: false);
                head.localPosition = new Vector3(0.1f, 1.55f, 0.2f);
                view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    go,
                    null,
                    null,
                    null);

                Assert.IsTrue(view.TryResolveControlStatusVfxAnchor(out Vector3 position, out Vector3 forward));

                Vector3 expected = head.position
                    + Vector3.up * CharacterTestRuntimeCharacterView.ControlStatusHeadBoneOffsetMeters;
                Assert.AreEqual(expected.x, position.x, 0.0001f);
                Assert.AreEqual(expected.y, position.y, 0.0001f);
                Assert.AreEqual(expected.z, position.z, 0.0001f);
                Assert.AreEqual(Vector3.right.x, forward.x, 0.0001f);
                Assert.AreEqual(Vector3.right.y, forward.y, 0.0001f);
                Assert.AreEqual(Vector3.right.z, forward.z, 0.0001f);
            }
            finally
            {
                view?.Release();
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
        }
}
}
