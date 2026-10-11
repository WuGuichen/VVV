using System;
using System.Collections;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    public sealed class CharacterTestDragon14BodyPartDamagePlayModeTests
    {
        private static readonly CharacterActionId LightAttackFollowUpActionId =
            new CharacterActionId(324);

        [UnityTest]
        public IEnumerator GeneratedDragon14_OpensAndBreaksHeadWithFrameStartDamageView()
        {
            yield return null;

            CharacterImportedGeometryBinding geometry = LoadDragon14Geometry(
                out CharacterTestCharacterDefinition dragonDefinition);
            Assert.AreEqual(CharacterBodyPartDamageMode.Authored, geometry.BodyPartDamageMode);
            Assert.AreEqual("mx.characterBodyPartDamage.v3", geometry.BodyPartDamageFormat);
            Assert.AreEqual("3.0", geometry.BodyPartDamageSchemaVersion);
            Assert.AreEqual(2, geometry.BodyPartDamageRevision);
            Assert.IsNotEmpty(geometry.BodyPartDamageContentHash);
            Assert.AreEqual(4, geometry.BodyPartDamageDefinitions.Length);
            Assert.AreEqual(10, geometry.BodyPartDamagePartLookup.Length);

            CharacterBodyPartDamageRuntimeBinding head = geometry.BodyPartDamageDefinitions[0];
            Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id162, head.PartStateId);
            Assert.AreEqual("partstate.000003.head", head.PartStateLabel);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id152, head.DamageZoneId);
            Assert.AreEqual(34, head.WoundCapacity);
            Assert.AreEqual(1_250, head.OpenDamageScaleMilli);
            Assert.AreEqual(68, head.MaxIntegrity);
            Assert.AreEqual(1_000, head.IntegrityDamageScaleMilli);
            Assert.IsEmpty(head.FocusConsumeTag,
                "Dragon14 intentionally has no authored Focus consume tag in M2 calibration.");
            Assert.IsEmpty(head.BreakReactionId,
                "Dragon14 intentionally projects a Broken transition without dispatching a reaction id.");

            Assert.AreEqual(51, geometry.BodyPartDamageDefinitions[1].WoundCapacity);
            Assert.AreEqual(85, geometry.BodyPartDamageDefinitions[1].MaxIntegrity);
            Assert.AreEqual(51, geometry.BodyPartDamageDefinitions[2].WoundCapacity);
            Assert.AreEqual(85, geometry.BodyPartDamageDefinitions[2].MaxIntegrity);
            Assert.AreEqual(68, geometry.BodyPartDamageDefinitions[3].WoundCapacity);
            Assert.AreEqual(90, geometry.BodyPartDamageDefinitions[3].MaxIntegrity);

            CharacterBodyColliderRuntimeBinding headCollider = FindBodyCollider(
                geometry,
                CharacterTestAuthoredContentIds.D400_Id150);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id3, headCollider.PartId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id104, headCollider.HitZoneId);
            Assert.IsTrue(headCollider.IsWeakPoint);
            Assert.AreEqual(1.25f, headCollider.DamageMultiplierOverride, 0.0001f,
                "The formal head collider is the 1.25 weak point; this test must not treat it as a 1.0 member.");
            CharacterTestHitZoneResolution headHitZone = HitZoneFromCollider(headCollider);
            CharacterTestBodyPartHitEvidence evidence = ExplicitEvidence(headHitZone);

            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity attacker = SpawnDefaultCharacter(world, teamId: 1, x: 0f);
            CharacterTestCharacterEntity target = Spawn(world, dragonDefinition, teamId: 2, x: 1f);
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                target.ReinitializeBodyPartState(geometry));
            Assert.IsTrue(target.BodyPartState.TryResolveDefinitionIndex(
                CharacterTestAuthoredContentIds.D400_Id3,
                out int headDefinitionIndex,
                out CharacterTestBodyPartEvidenceReason evidenceReason));
            Assert.AreEqual(CharacterTestBodyPartEvidenceReason.ExplicitBinding, evidenceReason);
            Assert.AreEqual(0, headDefinitionIndex);
            Assert.AreEqual(100, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
            Assert.AreEqual(CharacterTestCharacterLifeState.Alive, target.CombatState.LifeState);

            CharacterTestCharacterActionRuntime actionRuntime =
                CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                out CharacterActionConfig lightAttack));
            CombatTrackEvent hitWindow = FindHitWindow(
                lightAttack,
                CharacterTestAuthoredContentIds.D401_Id161);
            Assert.AreEqual(CharacterActionTrackEventKind.HitTraceWindow, hitWindow.Kind);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D401_Id161, hitWindow.StableEventId);
            Assert.AreEqual(6, hitWindow.DamagePayload.BaseDamage);
            Assert.AreEqual(DamageFormulaIds.CharacterTestSoftDefenseV1,
                hitWindow.DamagePayload.FormulaId);
            Assert.AreEqual(1, hitWindow.DamagePayload.SourceAttributeScales.Length);
            Assert.AreEqual(2.65f,
                hitWindow.DamagePayload.SourceAttributeScales[0].Scale,
                0.0001f,
                "The formal source, not the embedded fallback, owns the current light_attack scale.");
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                LightAttackFollowUpActionId,
                out CharacterActionConfig lightAttackFollowUp));
            CombatTrackEvent followUpHitWindow = FindHitWindow(
                lightAttackFollowUp,
                CharacterTestAuthoredContentIds.D401_Id165);
            Assert.AreEqual(1, followUpHitWindow.DamagePayload.SourceAttributeScales.Length);
            Assert.AreEqual(0.65f,
                followUpHitWindow.DamagePayload.SourceAttributeScales[0].Scale,
                0.0001f);
            Assert.IsTrue(CharacterTestDefaultWeaponTraceBindingResolver.Instance.TryResolve(
                attacker,
                hitWindow.EquipmentSlotId,
                hitWindow.TraceProfileId,
                out CharacterTestWeaponTraceBinding weaponBinding));
            Assert.Greater(weaponBinding.WeaponId, 0);

            var applier = new CharacterTestCombatDamageApplier();
            CharacterTestBodyPartDamageAuthorityBridge bridge = applier.BodyPartDamageBridge;
            const int OpenFrame = 100;
            var openFrame = new RuntimeFrame(OpenFrame);
            applier.ClearFrameOutputs();
            Assert.IsTrue(bridge.BeginAuthorityFrame(OpenFrame));
            DamageProfileResolveResult firstResolved = ResolveAndRecordActualLightAttackHit(
                world,
                attacker,
                target,
                bridge,
                lightAttack,
                hitWindow,
                weaponBinding,
                evidence,
                openFrame,
                hitIndex: 0,
                out DamageProfileResolveResult firstBase,
                out HitResolveResult firstHit,
                out CharacterTestBodyPartPreHitDecision openingDecision);
            Assert.AreEqual(66, firstBase.IncomingDamage,
                "The production resolver combines formal light_attack 2.65, live attributes, soft defense, and the 1.25 head weak point.");
            Assert.AreEqual(66, firstResolved.IncomingDamage);

            Assert.AreEqual(CharacterTestBodyPartWoundState.Closed,
                openingDecision.FrameStartState.WoundState);
            Assert.AreEqual(1_000, openingDecision.ScaleMilli);
            applier.Apply(
                world,
                actionRuntime,
                openFrame,
                new[] { firstHit },
                _ => firstResolved);
            Assert.AreEqual(1, bridge.DamageTelemetryCount);
            Assert.AreEqual(1, bridge.TransitionProjectionCount);
            CharacterTestBodyPartTransitionProjection opened = bridge.GetTransitionProjectionAt(0);
            Assert.AreEqual(CharacterTestBodyPartTransitionReason.Opened, opened.Transition.Reason);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id162, opened.Transition.PartStateId);
            Assert.IsFalse(opened.HasBreakReaction);
            Assert.AreEqual(34, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp),
                "The accepted 66-damage opener must pass through the production HP applier.");
            Assert.AreEqual(CharacterTestCharacterLifeState.Alive, target.CombatState.LifeState);
            Assert.AreEqual(0, applier.DeathEvents.Count);
            Assert.IsTrue(target.BodyPartState.TryGetCurrentEntry(
                0,
                out CharacterTestBodyPartStateEntry afterOpen));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Open, afterOpen.WoundState);
            Assert.AreEqual(34, afterOpen.WoundValue);
            Assert.AreEqual(CharacterTestBodyPartStructureState.Intact, afterOpen.StructureState);
            Assert.AreEqual(2, afterOpen.CurrentIntegrity);

            const int BreakFrame = OpenFrame + 1;
            var breakFrame = new RuntimeFrame(BreakFrame);
            applier.ClearFrameOutputs();
            Assert.IsTrue(bridge.BeginAuthorityFrame(BreakFrame));
            DamageProfileResolveResult breakingResolved = ResolveAndRecordActualLightAttackHit(
                world,
                attacker,
                target,
                bridge,
                lightAttackFollowUp,
                followUpHitWindow,
                weaponBinding,
                evidence,
                breakFrame,
                hitIndex: 1,
                out DamageProfileResolveResult breakingBase,
                out HitResolveResult breakingHit,
                out CharacterTestBodyPartPreHitDecision breakingDecision);
            Assert.AreEqual(21, breakingBase.IncomingDamage,
                "The formal light_attack_2 follow-up resolves through live attributes and the same 1.25 head weak point.");
            Assert.AreEqual(26, breakingResolved.IncomingDamage,
                "21 * 1250 / 1000 uses the Open body-part frame-start scale on the next authority frame.");
            Assert.AreEqual(CharacterTestBodyPartWoundState.Open,
                breakingDecision.FrameStartState.WoundState);
            Assert.AreEqual(1_250, breakingDecision.ScaleMilli);
            applier.Apply(
                world,
                actionRuntime,
                breakFrame,
                new[] { breakingHit },
                _ => breakingResolved);
            Assert.AreEqual(1, bridge.DamageTelemetryCount);
            Assert.AreEqual(1, bridge.TransitionProjectionCount);
            CharacterTestBodyPartTransitionProjection broken = bridge.GetTransitionProjectionAt(0);
            Assert.AreEqual(CharacterTestBodyPartTransitionReason.Broken, broken.Transition.Reason);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id162, broken.Transition.PartStateId);
            Assert.IsFalse(broken.HasBreakReaction,
                "The single Broken projection is explainable: formal Dragon14 leaves breakReactionId empty.");
            Assert.AreEqual(8, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp),
                "The calibrated head must break before the 100 HP target dies.");
            Assert.AreEqual(CharacterTestCharacterLifeState.Alive, target.CombatState.LifeState);
            Assert.AreEqual(0, applier.DeathEvents.Count);
            Assert.IsTrue(target.BodyPartState.TryGetCurrentEntry(
                0,
                out CharacterTestBodyPartStateEntry afterBreak));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Open, afterBreak.WoundState);
            Assert.AreEqual(CharacterTestBodyPartStructureState.Broken, afterBreak.StructureState);
            Assert.AreEqual(0, afterBreak.CurrentIntegrity);

            const int DeathFrame = BreakFrame + 1;
            var deathFrame = new RuntimeFrame(DeathFrame);
            applier.ClearFrameOutputs();
            Assert.IsTrue(bridge.BeginAuthorityFrame(DeathFrame));
            DamageProfileResolveResult deathResolved = ResolveAndRecordActualLightAttackHit(
                world,
                attacker,
                target,
                bridge,
                lightAttackFollowUp,
                followUpHitWindow,
                weaponBinding,
                evidence,
                deathFrame,
                hitIndex: 2,
                out DamageProfileResolveResult deathBase,
                out HitResolveResult deathHit,
                out CharacterTestBodyPartPreHitDecision deathDecision);
            Assert.AreEqual(21, deathBase.IncomingDamage);
            Assert.AreEqual(26, deathResolved.IncomingDamage);
            Assert.AreEqual(CharacterTestBodyPartStructureState.Broken,
                deathDecision.FrameStartState.StructureState);
            applier.Apply(
                world,
                actionRuntime,
                deathFrame,
                new[] { deathHit },
                _ => deathResolved);
            Assert.AreEqual(0, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
            Assert.AreEqual(CharacterTestCharacterLifeState.Dead, target.CombatState.LifeState,
                "The same resolver/applier chain must own the actual death transition after the calibrated break.");
            Assert.AreEqual(1, applier.DeathEvents.Count);
        }

        [UnityTest]
        public IEnumerator GeneratedDragon14_EachAuthoredGroupAccumulatesOnlyItsRealMember()
        {
            yield return null;

            CharacterImportedGeometryBinding geometry = LoadDragon14Geometry(
                out CharacterTestCharacterDefinition dragonDefinition);
            Assert.AreEqual(CharacterBodyPartDamageMode.Authored, geometry.BodyPartDamageMode);
            Assert.AreEqual(4, geometry.BodyPartDamageDefinitions.Length);
            int[] contractMemberPartIds =
            {
                CharacterTestAuthoredContentIds.D400_Id3,
                CharacterTestAuthoredContentIds.D400_Id31,
                CharacterTestAuthoredContentIds.D400_Id111,
                CharacterTestAuthoredContentIds.D400_Id103
            };
            int[] memberPartIds =
            {
                CharacterTestAuthoredContentIds.D400_Id3,
                CharacterTestAuthoredContentIds.D400_Id32,
                CharacterTestAuthoredContentIds.D400_Id97,
                CharacterTestAuthoredContentIds.D400_Id103
            };
            int[] partStateIds =
            {
                CharacterTestAuthoredContentIds.D400_Id162,
                CharacterTestAuthoredContentIds.D400_Id163,
                CharacterTestAuthoredContentIds.D400_Id164,
                CharacterTestAuthoredContentIds.D400_Id43
            };
            CharacterTestCharacterActionRuntime actionRuntime =
                CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                LightAttackFollowUpActionId,
                out CharacterActionConfig groupAction));
            CombatTrackEvent groupHitWindow = FindHitWindow(
                groupAction,
                CharacterTestAuthoredContentIds.D401_Id165);
            for (int groupIndex = 0; groupIndex < memberPartIds.Length; groupIndex++)
            {
                var world = new CharacterTestCharacterRuntimeWorld();
                CharacterTestCharacterEntity attacker = SpawnDefaultCharacter(
                    world,
                    teamId: 1,
                    x: 0f);
                CharacterTestCharacterEntity target = Spawn(
                    world,
                    dragonDefinition,
                    teamId: 2,
                    x: 1f);
                Assert.AreEqual(
                    CharacterTestBodyPartStateInitializationStatus.Ready,
                    target.ReinitializeBodyPartState(geometry));
                Assert.IsTrue(CharacterTestDefaultWeaponTraceBindingResolver.Instance.TryResolve(
                    attacker,
                    groupHitWindow.EquipmentSlotId,
                    groupHitWindow.TraceProfileId,
                    out CharacterTestWeaponTraceBinding weaponBinding));

                int memberPartId = memberPartIds[groupIndex];
                Assert.IsTrue(target.BodyPartState.TryResolveDefinitionIndex(
                    contractMemberPartIds[groupIndex],
                    out int contractDefinitionIndex,
                    out CharacterTestBodyPartEvidenceReason contractEvidenceReason));
                Assert.AreEqual(CharacterTestBodyPartEvidenceReason.ExplicitBinding,
                    contractEvidenceReason);
                Assert.AreEqual(groupIndex, contractDefinitionIndex,
                    contractMemberPartIds[groupIndex] + " must remain a formal member of its frozen group.");
                Assert.IsTrue(target.BodyPartState.TryResolveDefinitionIndex(
                    memberPartId,
                    out int definitionIndex,
                    out CharacterTestBodyPartEvidenceReason evidenceReason));
                Assert.AreEqual(CharacterTestBodyPartEvidenceReason.ExplicitBinding, evidenceReason);
                Assert.AreEqual(groupIndex, definitionIndex);
                Assert.AreEqual(partStateIds[groupIndex],
                    geometry.BodyPartDamageDefinitions[definitionIndex].PartStateId);

                var beforeWound = new int[memberPartIds.Length];
                var beforeIntegrity = new int[memberPartIds.Length];
                for (int entryIndex = 0; entryIndex < memberPartIds.Length; entryIndex++)
                {
                    Assert.IsTrue(target.BodyPartState.TryGetCurrentEntry(
                        entryIndex,
                        out CharacterTestBodyPartStateEntry before));
                    beforeWound[entryIndex] = before.WoundValue;
                    beforeIntegrity[entryIndex] = before.CurrentIntegrity;
                }

                int authorityFrame = 200 + groupIndex;
                var applier = new CharacterTestCombatDamageApplier();
                CharacterTestBodyPartDamageAuthorityBridge bridge = applier.BodyPartDamageBridge;
                CharacterTestBodyPartHitEvidence memberEvidence = FormalMemberEvidence(
                    geometry,
                    memberPartId);
                var frame = new RuntimeFrame(authorityFrame);
                applier.ClearFrameOutputs();
                Assert.IsTrue(bridge.BeginAuthorityFrame(authorityFrame));
                DamageProfileResolveResult resolved = ResolveAndRecordActualLightAttackHit(
                    world,
                    attacker,
                    target,
                    bridge,
                    groupAction,
                    groupHitWindow,
                    weaponBinding,
                    memberEvidence,
                    frame,
                    hitIndex: groupIndex,
                    out DamageProfileResolveResult baseResolved,
                    out HitResolveResult hit,
                    out CharacterTestBodyPartPreHitDecision decision);
                int expectedDamage = memberPartId == CharacterTestAuthoredContentIds.D400_Id3
                    ? 21
                    : 17;
                Assert.AreEqual(expectedDamage, baseResolved.IncomingDamage,
                    memberPartId + " must use the production light_attack_2 resolver and its actual collider multiplier.");
                Assert.AreEqual(expectedDamage, resolved.IncomingDamage);
                Assert.AreEqual(definitionIndex, decision.DefinitionIndex);
                applier.Apply(
                    world,
                    actionRuntime,
                    frame,
                    new[] { hit },
                    _ => resolved);
                Assert.AreEqual(
                    100 - expectedDamage,
                    world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
                Assert.AreEqual(CharacterTestCharacterLifeState.Alive, target.CombatState.LifeState);
                Assert.AreEqual(0, applier.DeathEvents.Count);
                Assert.AreEqual(1, bridge.DamageTelemetryCount);

                for (int entryIndex = 0; entryIndex < memberPartIds.Length; entryIndex++)
                {
                    Assert.IsTrue(target.BodyPartState.TryGetCurrentEntry(
                        entryIndex,
                        out CharacterTestBodyPartStateEntry after));
                    int targetDamage = entryIndex == definitionIndex ? expectedDamage : 0;
                    CharacterBodyPartDamageRuntimeBinding definition =
                        geometry.BodyPartDamageDefinitions[entryIndex];
                    int expectedWound = Math.Min(
                        definition.WoundCapacity,
                        beforeWound[entryIndex] + targetDamage);
                    int expectedIntegrity = Math.Max(
                        0,
                        beforeIntegrity[entryIndex] - targetDamage);
                    Assert.AreEqual(expectedWound, after.WoundValue,
                        $"{memberPartId} must change only {partStateIds[definitionIndex]} wound accumulation.");
                    Assert.AreEqual(expectedIntegrity, after.CurrentIntegrity,
                        $"{memberPartId} must change only {partStateIds[definitionIndex]} integrity.");
                }
            }
        }

        private static CharacterImportedGeometryBinding LoadDragon14Geometry(
            out CharacterTestCharacterDefinition dragonDefinition)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            for (int i = 0; i < registry.Definitions.Count; i++)
            {
                CharacterTestCharacterDefinition definition = registry.Definitions[i];
                if (!definition.GeneratedPackageRoot.Replace('\\', '/').EndsWith(
                        "/000003",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(
                    Path.GetFullPath(definition.GeneratedPackageRoot));
                Assert.NotNull(package);
                Assert.AreEqual("000003", package.PackageId);
                Assert.NotNull(package.Geometry);
                dragonDefinition = definition;
                return package.Geometry;
            }

            Assert.Fail("Dragon14 was not discovered as a playable generated package.");
            dragonDefinition = default;
            return null;
        }

        private static CharacterTestBodyPartHitEvidence FormalMemberEvidence(
            CharacterImportedGeometryBinding geometry,
            int memberPartId)
        {
            for (int i = 0; i < geometry.BodyColliders.Length; i++)
            {
                CharacterBodyColliderRuntimeBinding collider = geometry.BodyColliders[i];
                if (collider.PartId == memberPartId)
                    return ExplicitEvidence(HitZoneFromCollider(collider));
            }

            Assert.Fail("Formal Dragon14 playable collider member was not found: " + memberPartId);
            return default;
        }

        private static CharacterTestHitZoneResolution HitZoneFromCollider(
            CharacterBodyColliderRuntimeBinding collider)
        {
            return new CharacterTestHitZoneResolution(
                collider.HitZoneId,
                collider.PartId,
                CharacterTestReactionGroupIds.ResolveHeightGroup(
                    collider.HitZoneId,
                    collider.PartId,
                    0),
                collider.DamageMultiplierOverride > 0f
                    ? collider.DamageMultiplierOverride
                    : 1f,
                staggerScale: 1f,
                impulseScale: 1f,
                impactPowerScale: collider.ImpactPowerScaleOverride > 0f
                    ? collider.ImpactPowerScaleOverride
                    : 1f);
        }

        private static CharacterBodyColliderRuntimeBinding FindBodyCollider(
            CharacterImportedGeometryBinding geometry,
            int colliderId)
        {
            for (int i = 0; i < geometry.BodyColliders.Length; i++)
            {
                CharacterBodyColliderRuntimeBinding collider = geometry.BodyColliders[i];
                if (collider.ColliderId == colliderId)
                    return collider;
            }

            Assert.Fail("Formal Dragon14 collider was not found: " + colliderId);
            return null;
        }

        private static CombatTrackEvent FindHitWindow(
            CharacterActionConfig action,
            int stableEventId)
        {
            CombatTrackEvent[] events = action.CombatTrack.Events;
            for (int i = 0; i < events.Length; i++)
            {
                    if (events[i].Kind == CharacterActionTrackEventKind.HitTraceWindow
                        && events[i].StableEventId == stableEventId)
                {
                    return events[i];
                }
            }

            Assert.Fail("The production hit window was not found: " + stableEventId);
            return default;
        }

        private static CharacterTestCharacterEntity SpawnDefaultCharacter(
            CharacterTestCharacterRuntimeWorld world,
            int teamId,
            float x)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                out CharacterTestCharacterDefinition definition));
            return Spawn(world, definition, teamId, x);
        }

        private static CharacterTestCharacterEntity Spawn(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterDefinition definition,
            int teamId,
            float x)
        {
            return world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId,
                    x,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
        }

        private static DamageProfileResolveResult ResolveAndRecordActualLightAttackHit(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity attacker,
            CharacterTestCharacterEntity target,
            CharacterTestBodyPartDamageAuthorityBridge bridge,
            CharacterActionConfig lightAttack,
            CombatTrackEvent hitWindow,
            CharacterTestWeaponTraceBinding weaponBinding,
            CharacterTestBodyPartHitEvidence evidence,
            RuntimeFrame frame,
            int hitIndex,
            out DamageProfileResolveResult baseResolved,
            out HitResolveResult hit,
            out CharacterTestBodyPartPreHitDecision decision)
        {
            int authorityFrame = checked((int)frame.Value);
            int actionInstanceId = checked((authorityFrame * 10) + 1);
            int traceId = checked((authorityFrame * 10) + hitIndex + 1);
            var usage = new DamageProfileUsage(
                DamageProfileSourceKind.WeaponTrace,
                lightAttack.ActionId,
                actionInstanceId,
                hitWindow.StableEventId,
                weaponBinding.EquipmentSlotId,
                weaponBinding.WeaponId,
                attacker.Id.Value,
                target.Id.Value,
                evidence.HitZone.HitZoneId,
                evidence.HitZone.BodyPartId,
                authorityFrame,
                hitWindow.Frame);
            Assert.IsTrue(CharacterTestCombatDamageResolver.TryResolveCharacterActionDamage(
                hitWindow.DamagePayload,
                usage,
                attributeId => world.GetCurrentAttribute(attacker, attributeId, includeModifiers: true),
                attributeId => world.GetCurrentAttribute(target, attributeId, includeModifiers: true),
                evidence.HitZone,
                hitWindow.DamagePayload.PresentationProfileId,
                out baseResolved));
            DamageProfileResolveResult bodyPartResolved = bridge.ResolvePreHitDamage(
                authorityFrame,
                target.BodyPartState,
                evidence,
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                baseResolved,
                out decision);
            hit = new HitResolveResult(
                ToCombatEntityId(attacker),
                ToCombatEntityId(target),
                lightAttack.ActionId.Value,
                actionInstanceId,
                traceId,
                new CombatFrame(authorityFrame),
                HitResolveKind.Damage,
                bodyPartResolved.IncomingDamage,
                staggerFrames: 0,
                knockback: default);
            Assert.IsTrue(bridge.RecordResolvedHit(
                new CharacterTestResolvedHitKey(hit),
                target.BodyPartState,
                evidence,
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                attacker.Id.Value,
                target.Id.Value,
                policyEventId: traceId,
                localFrame: hitWindow.Frame,
                decision));
            return bodyPartResolved;
        }

        private static CombatEntityId ToCombatEntityId(CharacterTestCharacterEntity entity)
        {
            return new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value);
        }

        private static CharacterTestBodyPartHitEvidence ExplicitEvidence(
            CharacterTestHitZoneResolution hitZone)
        {
            return new CharacterTestBodyPartHitEvidence(
                hitZone,
                CharacterTestBodyPartHitProvenance.ExplicitColliderBinding);
        }
    }
}
