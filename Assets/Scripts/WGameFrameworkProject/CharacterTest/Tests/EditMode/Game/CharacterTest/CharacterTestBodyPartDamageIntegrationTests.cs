using System;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Projectiles;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestBodyPartDamageIntegrationTests
    {
        private const int TargetEntityId = 7;

        // Positive IDs reserved for synthetic integration fixtures.
        private const int FixtureBodyPartStateId = 201;
        private const int FixtureBodyPartId = 1;
        private const int FixtureBodyZoneId = 202;
        private const int FixtureBodyHitZoneId = 700;
        private const int FixtureUnknownPartStateId = 999;
        private const int FixtureStableEventId = 1001;
        private const int FixtureEquipmentSlotId = 1002;
        private const int FixtureWeaponId = 1003;

        [Test]
        public void PreHit_UsesOneFrameStartView_AndAppliesNewOpenStateNextFrame()
        {
            CharacterTestBodyPartStateComponent state = CreateReadyState(
                woundCapacity: 7,
                openDamageScaleMilli: 1_250);
            var bridge = new CharacterTestBodyPartDamageAuthorityBridge();
            CharacterTestBodyPartHitEvidence evidence = ExplicitEvidence("head");
            DamageProfileResolveResult damage = Damage(7);

            Assert.IsTrue(bridge.BeginAuthorityFrame(100));
            DamageProfileResolveResult hitA = bridge.ResolvePreHitDamage(
                100,
                state,
                evidence,
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                damage,
                out CharacterTestBodyPartPreHitDecision hitADecision);
            Assert.AreEqual(7, hitA.IncomingDamage);
            Assert.AreEqual(CharacterTestBodyPartWoundState.Closed, hitADecision.FrameStartState.WoundState);

            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageAcceptedDamage(AcceptedDamage(frame: 100, finalDamage: 7)));
            DamageProfileResolveResult hitB = bridge.ResolvePreHitDamage(
                100,
                state,
                evidence,
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                damage,
                out CharacterTestBodyPartPreHitDecision hitBDecision);
            Assert.AreEqual(7, hitB.IncomingDamage);
            Assert.AreEqual(CharacterTestBodyPartWoundState.Closed, hitBDecision.FrameStartState.WoundState);
            Assert.IsTrue(bridge.CompleteAuthorityFrame(100));

            Assert.IsTrue(bridge.BeginAuthorityFrame(101));
            DamageProfileResolveResult nextFrame = bridge.ResolvePreHitDamage(
                101,
                state,
                evidence,
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                damage,
                out CharacterTestBodyPartPreHitDecision nextDecision);
            Assert.AreEqual(8, nextFrame.IncomingDamage, "7 * 1250 / 1000 must use integer floor.");
            Assert.AreEqual(CharacterTestBodyPartWoundState.Open, nextDecision.FrameStartState.WoundState);
            Assert.AreEqual(1_250, nextDecision.ScaleMilli);
            Assert.IsTrue(nextDecision.Applied);
            Assert.IsTrue(bridge.CompleteAuthorityFrame(101));
        }

        [Test]
        public void PreHit_DefaultFallbackAndMissingDefinitions_PreserveLegacyDamage()
        {
            var bridge = new CharacterTestBodyPartDamageAuthorityBridge();
            var missingState = new CharacterTestBodyPartStateComponent(TargetEntityId);
            DamageProfileResolveResult damage = Damage(11);
            var fallback = new CharacterTestBodyPartHitEvidence(
                CharacterTestHitZoneResolution.Default,
                CharacterTestBodyPartHitProvenance.DefaultFallback);

            Assert.IsTrue(bridge.BeginAuthorityFrame(200));
            DamageProfileResolveResult fallbackDamage = bridge.ResolvePreHitDamage(
                200,
                missingState,
                fallback,
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                damage,
                out CharacterTestBodyPartPreHitDecision fallbackDecision);
            Assert.AreEqual(11, fallbackDamage.IncomingDamage);
            Assert.AreEqual(
                CharacterTestBodyPartDamageIgnoredReason.DefaultFallback,
                fallbackDecision.IgnoredReason);

            DamageProfileResolveResult noDefinitionsDamage = bridge.ResolvePreHitDamage(
                200,
                missingState,
                ExplicitEvidence("body"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                damage,
                out CharacterTestBodyPartPreHitDecision noDefinitionsDecision);
            Assert.AreEqual(11, noDefinitionsDamage.IncomingDamage);
            Assert.AreEqual(
                CharacterTestBodyPartDamageIgnoredReason.NoDefinitions,
                noDefinitionsDecision.IgnoredReason);
            Assert.IsTrue(bridge.CompleteAuthorityFrame(200));
        }

        [Test]
        public void DamageApplier_AppliesHpBeforeAcceptedBodyPartFactCommits()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity attacker = Spawn(world, teamId: 1, x: 0f);
            CharacterTestCharacterEntity target = Spawn(world, teamId: 2, x: 1f);
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                target.ReinitializeBodyPartState(CreateBinding(
                    woundCapacity: 5,
                    openDamageScaleMilli: 1_250)));

            var applier = new CharacterTestCombatDamageApplier();
            CharacterTestBodyPartDamageAuthorityBridge bridge = applier.BodyPartDamageBridge;
            var frame = new RuntimeFrame(300);
            HitResolveResult hit = Hit(attacker, target, frame, damage: 5);
            DamageProfileResolveResult payload = Damage(
                5,
                Usage(attacker, target, hit, localFrame: 3));
            DamageProfileResolveResult resolved = bridge.ResolvePreHitDamage(
                frame.Value,
                target.BodyPartState,
                ExplicitEvidence("head"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                payload,
                out CharacterTestBodyPartPreHitDecision decision);
            Assert.IsTrue(bridge.RecordResolvedHit(
                new CharacterTestResolvedHitKey(hit),
                target.BodyPartState,
                ExplicitEvidence("head"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                attacker.Id.Value,
                target.Id.Value,
                policyEventId: 4_242,
                localFrame: 3,
                decision));

            int hpBefore = world.GetCurrentAttribute(
                target,
                CharacterTestCharacterAttributeIds.Hp);
            applier.Apply(
                world,
                actionRuntime: null,
                frame,
                new[] { hit },
                _ => resolved);

            Assert.AreEqual(
                hpBefore - 5,
                world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
            Assert.IsTrue(target.BodyPartState.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry entry));
            Assert.AreEqual(5, entry.WoundValue);
            Assert.AreEqual(CharacterTestBodyPartWoundState.Open, entry.WoundState);
            Assert.AreEqual(1, target.BodyPartState.TransitionCount);
            Assert.AreEqual(4_242, target.BodyPartState.GetTransitionAt(0).PolicyEventId);
            Assert.AreEqual(1, bridge.DamageTelemetryCount);
            Assert.AreEqual(1, bridge.TransitionProjectionCount);
        }

        [Test]
        public void AcceptedDamageBridge_DedupesRepeatedResolvedEventBeforeReducer()
        {
            CharacterTestBodyPartStateComponent state = CreateReadyState(
                woundCapacity: 100,
                openDamageScaleMilli: 1_250);
            var bridge = new CharacterTestBodyPartDamageAuthorityBridge();
            var hit = new HitResolveResult(
                new CombatEntityId(101),
                new CombatEntityId(107),
                actionId: 11,
                actionInstanceId: 22,
                traceId: 33,
                frame: new CombatFrame(400),
                HitResolveKind.Damage,
                damage: 5,
                staggerFrames: 0,
                knockback: default);
            DamageProfileResolveResult payload = Damage(
                5,
                new DamageProfileUsage(
                    DamageProfileSourceKind.WeaponTrace,
                    new CharacterActionId(hit.ActionId),
                    hit.ActionInstanceId,
                    FixtureStableEventId,
                    FixtureEquipmentSlotId,
                    FixtureWeaponId,
                    sourceEntityId: 1,
                    targetEntityId: TargetEntityId,
                    hitZoneId: CharacterTestAuthoredContentIds.D400_Id56,
                    bodyPartId: CharacterTestAuthoredContentIds.D400_Id3,
                    frame: hit.Frame.Value,
                    localFrame: 4));
            Assert.IsTrue(bridge.BeginAuthorityFrame(400));
            DamageProfileResolveResult resolved = bridge.ResolvePreHitDamage(
                400,
                state,
                ExplicitEvidence("head"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                payload,
                out CharacterTestBodyPartPreHitDecision decision);
            Assert.IsTrue(bridge.RecordResolvedHit(
                new CharacterTestResolvedHitKey(hit),
                state,
                ExplicitEvidence("head"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                sourceEntityId: 1,
                targetEntityId: TargetEntityId,
                policyEventId: 44,
                localFrame: 4,
                decision));
            Assert.IsTrue(bridge.RecordResolvedHit(
                new CharacterTestResolvedHitKey(hit),
                state,
                ExplicitEvidence("head"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                sourceEntityId: 1,
                targetEntityId: TargetEntityId,
                policyEventId: 44,
                localFrame: 4,
                decision), "An exact profiling duplicate must be an idempotent no-op.");
            var damageEvent = new DamageResolvedEvent(
                new DamageRequest(resolved.DamageProfileId, resolved.Usage),
                resolved,
                hit,
                finalDamage: 5);

            Assert.IsTrue(bridge.CacheResolvedDamageEvent(damageEvent));
            Assert.IsTrue(bridge.CacheResolvedDamageEvent(damageEvent));
            Assert.IsTrue(state.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry before));
            Assert.AreEqual(0, before.WoundValue, "The pre-HP callback may only cache.");
            Assert.IsTrue(bridge.CompleteAuthorityFrame(400));
            Assert.IsTrue(state.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry after));
            Assert.AreEqual(5, after.WoundValue);
        }

        [Test]
        public void AcceptedDamageBridge_ConflictingResolvedIdentityFailsClosed()
        {
            CharacterTestBodyPartStateComponent state = CreateReadyState(
                woundCapacity: 100,
                openDamageScaleMilli: 1_250);
            var bridge = new CharacterTestBodyPartDamageAuthorityBridge();
            var hit = new HitResolveResult(
                new CombatEntityId(101),
                new CombatEntityId(107),
                actionId: 11,
                actionInstanceId: 22,
                traceId: 33,
                frame: new CombatFrame(500),
                HitResolveKind.Damage,
                damage: 5,
                staggerFrames: 0,
                knockback: default);
            DamageProfileResolveResult payload = Damage(
                5,
                new DamageProfileUsage(
                    DamageProfileSourceKind.WeaponTrace,
                    new CharacterActionId(hit.ActionId),
                    hit.ActionInstanceId,
                    FixtureStableEventId,
                    FixtureEquipmentSlotId,
                    FixtureWeaponId,
                    sourceEntityId: 1,
                    targetEntityId: TargetEntityId,
                    hitZoneId: CharacterTestAuthoredContentIds.D400_Id56,
                    bodyPartId: CharacterTestAuthoredContentIds.D400_Id3,
                    frame: hit.Frame.Value,
                    localFrame: 4));
            Assert.IsTrue(bridge.BeginAuthorityFrame(500));
            DamageProfileResolveResult resolved = bridge.ResolvePreHitDamage(
                500,
                state,
                ExplicitEvidence("head"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                payload,
                out CharacterTestBodyPartPreHitDecision decision);
            var key = new CharacterTestResolvedHitKey(hit);
            Assert.IsTrue(bridge.RecordResolvedHit(
                key,
                state,
                ExplicitEvidence("head"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                sourceEntityId: 1,
                targetEntityId: TargetEntityId,
                policyEventId: 44,
                localFrame: 4,
                decision));
            Assert.IsFalse(bridge.RecordResolvedHit(
                key,
                state,
                ExplicitEvidence("head"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                sourceEntityId: 1,
                targetEntityId: TargetEntityId,
                policyEventId: 45,
                localFrame: 4,
                decision));
            Assert.IsTrue(bridge.CacheResolvedDamageEvent(new DamageResolvedEvent(
                new DamageRequest(resolved.DamageProfileId, resolved.Usage),
                resolved,
                hit,
                finalDamage: 5)));

            Assert.IsTrue(bridge.CompleteAuthorityFrame(500));
            Assert.IsTrue(state.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry after));
            Assert.AreEqual(0, after.WoundValue);
        }

        [TestCase((int)CharacterTestBodyPartDamageSourcePolicy.DirectTrace, true)]
        [TestCase((int)CharacterTestBodyPartDamageSourcePolicy.Projectile, true)]
        [TestCase((int)CharacterTestBodyPartDamageSourcePolicy.Ability, true)]
        [TestCase((int)CharacterTestBodyPartDamageSourcePolicy.NoPartArea, false)]
        [TestCase((int)CharacterTestBodyPartDamageSourcePolicy.StatusTick, false)]
        [TestCase((int)CharacterTestBodyPartDamageSourcePolicy.Unsupported, false)]
        public void AcceptedDamageBridge_SourcePolicyIsTypedAndFailClosed(
            int sourcePolicyValue,
            bool accumulates)
        {
            var sourcePolicy = (CharacterTestBodyPartDamageSourcePolicy)sourcePolicyValue;
            int woundValue = ResolveAcceptedBridgeFact(
                sourcePolicy,
                CharacterTestBodyPartHitProvenance.ExplicitColliderBinding,
                frame: 600 + (int)sourcePolicy);
            Assert.AreEqual(accumulates ? 5 : 0, woundValue);
        }

        [TestCase((int)CharacterTestBodyPartHitProvenance.DefaultFallback)]
        [TestCase((int)CharacterTestBodyPartHitProvenance.NoPartEvidence)]
        [TestCase((int)CharacterTestBodyPartHitProvenance.InvalidEvidence)]
        public void AcceptedDamageBridge_NonExplicitProvenanceNeverAccumulates(
            int provenanceValue)
        {
            var provenance = (CharacterTestBodyPartHitProvenance)provenanceValue;
            Assert.AreEqual(
                0,
                ResolveAcceptedBridgeFact(
                    CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                    provenance,
                    frame: 700 + (int)provenance));
        }

        [Test]
        public void ExplicitAuthoredBody_IsAcceptedWhileSameValueFallbackIsRejected()
        {
            Assert.AreEqual(
                5,
                ResolveAcceptedBridgeFact(
                    CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                    CharacterTestBodyPartHitProvenance.ExplicitColliderBinding,
                    frame: 710,
                    bodyPartId: "body"));
            Assert.AreEqual(
                0,
                ResolveAcceptedBridgeFact(
                    CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                    CharacterTestBodyPartHitProvenance.DefaultFallback,
                    frame: 711,
                    bodyPartId: "body"));
        }

        [Test]
        public void ProjectileSourcePolicy_AcceptsDirectImpactAndRejectsAreaPulse()
        {
            Assert.AreEqual(
                CharacterTestBodyPartDamageSourcePolicy.Projectile,
                CharacterTestCombatDamageResolver.ResolveProjectileBodyPartSourcePolicy(
                    new CombatProjectileImpactContext(
                        CombatProjectileImpactSourceKind.DirectSweep)));
            Assert.AreEqual(
                CharacterTestBodyPartDamageSourcePolicy.NoPartArea,
                CharacterTestCombatDamageResolver.ResolveProjectileBodyPartSourcePolicy(
                    new CombatProjectileImpactContext(
                        CombatProjectileImpactSourceKind.AreaPulse)));
        }

        [Test]
        public void FocusIngress_SameFrameNewOpenCannotConsume_ThenConsumesOnceNextFrame()
        {
            CharacterTestBodyPartStateComponent state = CreateReadyState(
                woundCapacity: 5,
                openDamageScaleMilli: 1_250);
            var bridge = new CharacterTestBodyPartDamageAuthorityBridge();

            Assert.IsTrue(bridge.BeginAuthorityFrame(800));
            bridge.ResolvePreHitDamage(
                800,
                state,
                ExplicitEvidence("head"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                Damage(5),
                out _);
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageAcceptedDamage(AcceptedDamage(frame: 800, finalDamage: 5)));
            Assert.AreEqual(
                CharacterTestBodyPartFocusConsumeIngressResult.StateNotOpenAtFrameStart,
                bridge.StageAcceptedFocusConsume(
                    state,
                    FocusIngress(
                        frame: 800,
                        CharacterTestBodyPartFocusConsumeAdapterKind.Action,
                        correlationId: 80)));
            Assert.IsTrue(bridge.CompleteAuthorityFrame(800));
            Assert.IsTrue(state.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry opened));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Open, opened.WoundState);

            Assert.IsTrue(bridge.BeginAuthorityFrame(801));
            CharacterTestBodyPartFocusConsumeIngress focus = FocusIngress(
                frame: 801,
                CharacterTestBodyPartFocusConsumeAdapterKind.Action,
                correlationId: 81);
            Assert.AreEqual(
                CharacterTestBodyPartFocusConsumeIngressResult.Staged,
                bridge.StageAcceptedFocusConsume(state, focus));
            Assert.AreEqual(
                CharacterTestBodyPartFocusConsumeIngressResult.Duplicate,
                bridge.StageAcceptedFocusConsume(state, focus));
            Assert.IsTrue(bridge.CompleteAuthorityFrame(801));
            Assert.IsTrue(state.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry consumed));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Consumed, consumed.WoundState);
            Assert.AreEqual(1, state.TransitionCount);
            Assert.AreEqual(
                CharacterTestBodyPartTransitionReason.Consumed,
                state.GetTransitionAt(0).Reason);
        }

        [TestCase((int)CharacterTestBodyPartFocusConsumeAdapterKind.Action)]
        [TestCase((int)CharacterTestBodyPartFocusConsumeAdapterKind.Ability)]
        [TestCase((int)CharacterTestBodyPartFocusConsumeAdapterKind.Interaction)]
        public void FocusIngress_OnlyExplicitAcceptedAdaptersMayStage(int adapterKindValue)
        {
            CharacterTestBodyPartStateComponent state = CreateOpenState(frame: 820);
            var bridge = new CharacterTestBodyPartDamageAuthorityBridge();
            var adapterKind = (CharacterTestBodyPartFocusConsumeAdapterKind)adapterKindValue;

            Assert.AreEqual(
                CharacterTestBodyPartFocusConsumeIngressResult.Staged,
                bridge.StageAcceptedFocusConsume(
                    state,
                    FocusIngress(frame: 821, adapterKind, correlationId: 821)));
            Assert.IsTrue(bridge.CompleteAuthorityFrame(821));
            Assert.IsTrue(state.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry consumed));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Consumed, consumed.WoundState);
        }

        [Test]
        public void FocusIngress_RejectedStaleUnknownAndDamageTagFailClosed()
        {
            CharacterTestBodyPartStateComponent state = CreateOpenState(frame: 840);
            var bridge = new CharacterTestBodyPartDamageAuthorityBridge();
            Assert.IsTrue(bridge.BeginAuthorityFrame(841));

            Assert.AreEqual(
                CharacterTestBodyPartFocusConsumeIngressResult.AuthorityRejected,
                bridge.StageAcceptedFocusConsume(
                    state,
                    FocusIngress(
                        frame: 841,
                        CharacterTestBodyPartFocusConsumeAdapterKind.Ability,
                        correlationId: 1,
                        authorityAccepted: false)));
            Assert.AreEqual(
                CharacterTestBodyPartFocusConsumeIngressResult.UnsupportedAdapter,
                bridge.StageAcceptedFocusConsume(
                    state,
                    FocusIngress(
                        frame: 841,
                        CharacterTestBodyPartFocusConsumeAdapterKind.None,
                        correlationId: 2)));
            Assert.AreEqual(
                CharacterTestBodyPartFocusConsumeIngressResult.FrameMismatch,
                bridge.StageAcceptedFocusConsume(
                    state,
                    FocusIngress(
                        frame: 840,
                        CharacterTestBodyPartFocusConsumeAdapterKind.Action,
                        correlationId: 3)));
            Assert.AreEqual(
                CharacterTestBodyPartFocusConsumeIngressResult.UnknownPartState,
                bridge.StageAcceptedFocusConsume(
                    state,
                    new CharacterTestBodyPartFocusConsumeIngress(
                        authorityFrame: 841,
                        CharacterTestBodyPartFocusConsumeAdapterKind.Interaction,
                        sourceEntityId: 1,
                        targetEntityId: TargetEntityId,
                        partStateId: FixtureUnknownPartStateId,
                        focusConsumeTag: "action.focus.finish",
                        correlationId: 4,
                        authorityAccepted: true)));
            Assert.AreEqual(
                CharacterTestBodyPartFocusConsumeIngressResult.TagMismatch,
                bridge.StageAcceptedFocusConsume(
                    state,
                    new CharacterTestBodyPartFocusConsumeIngress(
                        authorityFrame: 841,
                        CharacterTestBodyPartFocusConsumeAdapterKind.Action,
                        sourceEntityId: 1,
                        targetEntityId: TargetEntityId,
                        partStateId: CharacterTestAuthoredContentIds.D400_Id162,
                        focusConsumeTag: "damage.focus.finish",
                        correlationId: 5,
                        authorityAccepted: true)));

            Assert.IsTrue(bridge.CompleteAuthorityFrame(841));
            Assert.IsTrue(state.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry stillOpen));
            Assert.AreEqual(CharacterTestBodyPartWoundState.Open, stillOpen.WoundState);
        }

        [TestCase((int)HitResolveKind.TargetDead, 5, 0, (int)CharacterTestBodyPartDamageIgnoredReason.TargetDead)]
        [TestCase((int)HitResolveKind.Invincible, 5, 0, (int)CharacterTestBodyPartDamageIgnoredReason.Invincible)]
        [TestCase((int)HitResolveKind.Parried, 5, 0, (int)CharacterTestBodyPartDamageIgnoredReason.Parried)]
        [TestCase((int)HitResolveKind.Blocked, 5, 0, (int)CharacterTestBodyPartDamageIgnoredReason.Blocked)]
        [TestCase((int)HitResolveKind.Duplicate, 5, 0, (int)CharacterTestBodyPartDamageIgnoredReason.Duplicate)]
        [TestCase((int)HitResolveKind.SelfDamage, 5, 0, (int)CharacterTestBodyPartDamageIgnoredReason.SelfTarget)]
        [TestCase((int)HitResolveKind.Friendly, 5, 0, (int)CharacterTestBodyPartDamageIgnoredReason.FriendlyTarget)]
        [TestCase((int)HitResolveKind.Damage, 0, 0, (int)CharacterTestBodyPartDamageIgnoredReason.RejectedHit)]
        [TestCase((int)HitResolveKind.Damage, 5, 0, (int)CharacterTestBodyPartDamageIgnoredReason.ZeroFinalDamage)]
        public void AcceptedDamageBridge_RejectedOutcomeMatrixFailsClosed(
            int hitKindValue,
            int hitDamage,
            int finalDamage,
            int expectedReasonValue)
        {
            CharacterTestBodyPartDamageTelemetryProjection telemetry =
                ResolveBridgeOutcome(
                    (HitResolveKind)hitKindValue,
                    hitDamage,
                    finalDamage,
                    frame: 900 + hitKindValue + hitDamage);

            Assert.AreEqual(
                (CharacterTestBodyPartDamageIgnoredReason)expectedReasonValue,
                telemetry.IgnoredReason);
            Assert.AreEqual(finalDamage, telemetry.FinalDamage);
            Assert.IsTrue(telemetry.HasCommittedState);
            Assert.AreEqual(0, telemetry.CommittedState.WoundValue);
        }

        [TestCase(
            (int)CharacterTestBodyPartDamageSourcePolicy.NoPartArea,
            (int)CharacterTestBodyPartDamageIgnoredReason.NoPartArea)]
        [TestCase(
            (int)CharacterTestBodyPartDamageSourcePolicy.StatusTick,
            (int)CharacterTestBodyPartDamageIgnoredReason.StatusTick)]
        public void IgnoredSourceOnlyFrame_StillProducesTypedTelemetry(
            int sourcePolicyValue,
            int expectedReasonValue)
        {
            const int frame = 940;
            CharacterTestBodyPartStateComponent state = CreateReadyState(
                woundCapacity: 100,
                openDamageScaleMilli: 1_250);
            var bridge = new CharacterTestBodyPartDamageAuthorityBridge();
            var hit = new HitResolveResult(
                new CombatEntityId(101),
                new CombatEntityId(107),
                actionId: 11,
                actionInstanceId: 22,
                traceId: 33,
                frame: new CombatFrame(frame),
                HitResolveKind.Damage,
                damage: 5,
                staggerFrames: 0,
                knockback: default);
            DamageProfileResolveResult payload = Damage(
                5,
                Usage(
                    sourceEntityId: 1,
                    targetEntityId: TargetEntityId,
                    hit,
                    localFrame: 4));
            var sourcePolicy = (CharacterTestBodyPartDamageSourcePolicy)sourcePolicyValue;

            DamageProfileResolveResult resolved = bridge.ResolvePreHitDamage(
                frame,
                state,
                ExplicitEvidence("head"),
                sourcePolicy,
                payload,
                out CharacterTestBodyPartPreHitDecision decision);
            Assert.IsTrue(bridge.RecordResolvedHit(
                new CharacterTestResolvedHitKey(hit),
                state,
                ExplicitEvidence("head"),
                sourcePolicy,
                sourceEntityId: 1,
                targetEntityId: TargetEntityId,
                policyEventId: 44,
                localFrame: 4,
                decision));
            Assert.IsTrue(bridge.CacheResolvedDamageEvent(new DamageResolvedEvent(
                new DamageRequest(resolved.DamageProfileId, resolved.Usage),
                resolved,
                hit,
                finalDamage: 5)));
            Assert.IsTrue(bridge.CompleteAuthorityFrame(frame));

            Assert.AreEqual(1, bridge.DamageTelemetryCount);
            Assert.AreEqual(
                (CharacterTestBodyPartDamageIgnoredReason)expectedReasonValue,
                bridge.GetDamageTelemetryAt(0).IgnoredReason);
            Assert.IsTrue(state.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry entry));
            Assert.AreEqual(0, entry.WoundValue);
        }

        [Test]
        public void Commit_ProjectsDamageAndBreakReactionOnce_ThenEmptyFrameClearsOutputs()
        {
            const int frame = 950;
            CharacterTestBodyPartStateComponent state = CreateReadyState(
                woundCapacity: 5,
                openDamageScaleMilli: 1_250);
            var bridge = new CharacterTestBodyPartDamageAuthorityBridge();
            var hit = new HitResolveResult(
                new CombatEntityId(101),
                new CombatEntityId(107),
                actionId: 11,
                actionInstanceId: 22,
                traceId: 33,
                frame: new CombatFrame(frame),
                HitResolveKind.Damage,
                damage: 100,
                staggerFrames: 0,
                knockback: default);
            DamageProfileResolveResult payload = Damage(
                100,
                Usage(
                    sourceEntityId: 1,
                    targetEntityId: TargetEntityId,
                    hit,
                    localFrame: 4));

            Assert.IsTrue(bridge.BeginAuthorityFrame(frame));
            DamageProfileResolveResult resolved = bridge.ResolvePreHitDamage(
                frame,
                state,
                ExplicitEvidence("head"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                payload,
                out CharacterTestBodyPartPreHitDecision decision);
            Assert.IsTrue(bridge.RecordResolvedHit(
                new CharacterTestResolvedHitKey(hit),
                state,
                ExplicitEvidence("head"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                sourceEntityId: 1,
                targetEntityId: TargetEntityId,
                policyEventId: 44,
                localFrame: 4,
                decision));
            Assert.IsTrue(bridge.CacheResolvedDamageEvent(new DamageResolvedEvent(
                new DamageRequest(resolved.DamageProfileId, resolved.Usage),
                resolved,
                hit,
                finalDamage: 100)));
            Assert.IsTrue(bridge.CompleteAuthorityFrame(frame));

            Assert.AreEqual(1, bridge.DamageTelemetryCount);
            CharacterTestBodyPartDamageTelemetryProjection telemetry =
                bridge.GetDamageTelemetryAt(0);
            Assert.AreEqual(CharacterTestBodyPartDamageIgnoredReason.None, telemetry.IgnoredReason);
            Assert.AreEqual(100, telemetry.BaseDamage);
            Assert.AreEqual(100, telemetry.ResolvedDamage);
            Assert.AreEqual(100, telemetry.FinalDamage);
            Assert.AreEqual(CharacterTestBodyPartWoundState.Closed, telemetry.FrameStartState.WoundState);
            Assert.IsTrue(telemetry.HasCommittedState);
            Assert.AreEqual(CharacterTestBodyPartWoundState.Open, telemetry.CommittedState.WoundState);
            Assert.AreEqual(CharacterTestBodyPartStructureState.Broken, telemetry.CommittedState.StructureState);

            Assert.AreEqual(2, bridge.TransitionProjectionCount);
            int reactionCount = 0;
            for (int i = 0; i < bridge.TransitionProjectionCount; i++)
            {
                CharacterTestBodyPartTransitionProjection projection =
                    bridge.GetTransitionProjectionAt(i);
                if (projection.HasBreakReaction)
                {
                    reactionCount++;
                    Assert.AreEqual("reaction.break", projection.BreakReactionId);
                }
            }
            Assert.AreEqual(1, reactionCount);

            Assert.IsTrue(bridge.CompleteAuthorityFrame(frame + 1));
            Assert.AreEqual(0, bridge.DamageTelemetryCount);
            Assert.AreEqual(0, bridge.TransitionProjectionCount);
        }

        [Test]
        public void AcceptedDamageBridge_WarmedResolveCacheComplete4096_AllocatesZeroBytes()
        {
            CharacterTestBodyPartStateComponent state = CreateReadyState(
                woundCapacity: 1_000_000,
                openDamageScaleMilli: 1_250);
            var bridge = new CharacterTestBodyPartDamageAuthorityBridge();
            CharacterTestBodyPartHitEvidence evidence = ExplicitEvidence("head");
            Assert.IsTrue(RunAcceptedDamageFrame(bridge, state, evidence, frame: 1));

            bool succeeded = false;
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 2; i < 4_098; i++)
                succeeded = RunAcceptedDamageFrame(bridge, state, evidence, i);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(succeeded);
            Assert.AreEqual(0L, allocated);
        }

        private static CharacterTestBodyPartStateComponent CreateReadyState(
            int woundCapacity,
            int openDamageScaleMilli,
            string bodyPartId = "head")
        {
            var state = new CharacterTestBodyPartStateComponent(TargetEntityId);
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                state.Reinitialize(CreateBinding(
                    woundCapacity,
                    openDamageScaleMilli,
                    bodyPartId)));
            return state;
        }

        private static CharacterTestBodyPartStateComponent CreateOpenState(long frame)
        {
            CharacterTestBodyPartStateComponent state = CreateReadyState(
                woundCapacity: 5,
                openDamageScaleMilli: 1_250);
            Assert.IsTrue(state.TryBeginFrame(frame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageAcceptedDamage(AcceptedDamage(frame, finalDamage: 5)));
            Assert.IsTrue(state.CommitFrame().Succeeded);
            return state;
        }

        private static CharacterTestBodyPartFocusConsumeIngress FocusIngress(
            long frame,
            CharacterTestBodyPartFocusConsumeAdapterKind adapterKind,
            long correlationId,
            bool authorityAccepted = true)
        {
            return new CharacterTestBodyPartFocusConsumeIngress(
                frame,
                adapterKind,
                sourceEntityId: 1,
                targetEntityId: TargetEntityId,
                partStateId: CharacterTestAuthoredContentIds.D400_Id162,
                focusConsumeTag: "action.focus.finish",
                correlationId,
                authorityAccepted);
        }

        private static CharacterImportedGeometryBinding CreateBinding(
            int woundCapacity,
            int openDamageScaleMilli,
            string bodyPartId = "head")
        {
            bool body = string.Equals(bodyPartId, "body", StringComparison.Ordinal);
            var definition = new CharacterBodyPartDamageRuntimeBinding(
                definitionIndex: 0,
                partStateId: body ? FixtureBodyPartStateId : CharacterTestAuthoredContentIds.D400_Id162,
                memberPartIds: new[] { body ? FixtureBodyPartId : CharacterTestAuthoredContentIds.D400_Id3 },
                damageZoneId: body ? FixtureBodyZoneId : CharacterTestAuthoredContentIds.D400_Id152,
                woundCapacity: woundCapacity,
                openDamageScaleMilli: openDamageScaleMilli,
                focusConsumeTag: "action.focus.finish",
                maxIntegrity: 100,
                integrityDamageScaleMilli: 1_000,
                breakReactionId: "reaction.break",
                authoredPriorityDeltaOpen: 10,
                authoredPriorityDeltaBroken: 20,
                partStateLabel: body ? "part.body" : "part.head",
                damageZoneLabel: body ? "zone.body" : "zone.head",
                memberPartLabels: new[] { bodyPartId });
            return new CharacterImportedGeometryBinding(
                format: "mx.characterGeometryBinding.v5",
                schemaVersion: "5.0",
                packageId: "test.body-part-damage-integration",
                bodyProfile: null,
                bodyColliders: Array.Empty<CharacterBodyColliderRuntimeBinding>(),
                hitZoneBindings: Array.Empty<CharacterHitZoneRuntimeBinding>(),
                damageZones: Array.Empty<CharacterDamageZoneRuntimeBinding>(),
                sockets: Array.Empty<CharacterSocketRuntimeBinding>(),
                weaponAttachments: Array.Empty<CharacterWeaponAttachmentRuntimeBinding>(),
                weaponTraces: Array.Empty<CharacterWeaponTraceRuntimeBinding>(),
                bodyPartDamageDefinitions: new[] { definition },
                bodyPartDamagePartLookup: new[]
                {
                    new CharacterBodyPartDamageLookupRuntimeBinding(body ? 1 : CharacterTestAuthoredContentIds.D400_Id3, 0, bodyPartId)
                },
                bodyPartDamageFormat: "mx.characterBodyPartDamage.v1",
                bodyPartDamageSchemaVersion: "1.0",
                bodyPartDamageRevision: 1,
                bodyPartDamageContentHash: "sha256:test",
                bodyPartDamageMode: CharacterBodyPartDamageMode.Authored);
        }

        private static CharacterTestBodyPartHitEvidence ExplicitEvidence(string bodyPartId)
        {
            return new CharacterTestBodyPartHitEvidence(
                new CharacterTestHitZoneResolution(
                    bodyPartId == "body" ? FixtureBodyHitZoneId : CharacterTestAuthoredContentIds.D400_Id56,
                    bodyPartId == "body" ? FixtureBodyPartId : CharacterTestAuthoredContentIds.D400_Id3,
                    CharacterTestReactionGroupIds.HeightMid,
                    damageMultiplier: 1f,
                    staggerScale: 1f,
                    impulseScale: 1f,
                    impactPowerScale: 1f),
                CharacterTestBodyPartHitProvenance.ExplicitColliderBinding);
        }

        private static DamageProfileResolveResult Damage(int incomingDamage)
        {
            return Damage(incomingDamage, default);
        }

        private static DamageProfileResolveResult Damage(
            int incomingDamage,
            DamageProfileUsage usage)
        {
            return new DamageProfileResolveResult(
                success: true,
                reason: string.Empty,
                damageProfileId: 0,
                damageTypeId: CharacterTestAuthoredContentIds.D409_Id2,
                formulaId: DamageFormulaIds.Flat,
                incomingDamage: incomingDamage,
                staggerFrames: 0,
                impactForce: 0,
                knockback: default,
                impactPower: 0,
                guardPressure: 0,
                presentationProfileId: 0,
                usage: usage);
        }

        private static CharacterTestCharacterEntity Spawn(
            CharacterTestCharacterRuntimeWorld world,
            int teamId,
            float x)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                out CharacterTestCharacterDefinition definition));
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

        private static HitResolveResult Hit(
            CharacterTestCharacterEntity attacker,
            CharacterTestCharacterEntity target,
            RuntimeFrame frame,
            int damage)
        {
            return new HitResolveResult(
                ToCombatEntityId(attacker),
                ToCombatEntityId(target),
                actionId: 11,
                actionInstanceId: 22,
                traceId: 33,
                frame: new CombatFrame((int)frame.Value),
                HitResolveKind.Damage,
                damage,
                staggerFrames: 0,
                knockback: default);
        }

        private static DamageProfileUsage Usage(
            CharacterTestCharacterEntity attacker,
            CharacterTestCharacterEntity target,
            HitResolveResult hit,
            int localFrame)
        {
            return new DamageProfileUsage(
                DamageProfileSourceKind.WeaponTrace,
                new CharacterActionId(hit.ActionId),
                hit.ActionInstanceId,
                FixtureStableEventId,
                FixtureEquipmentSlotId,
                FixtureWeaponId,
                attacker.Id.Value,
                target.Id.Value,
                CharacterTestAuthoredContentIds.D400_Id56,
                CharacterTestAuthoredContentIds.D400_Id3,
                hit.Frame.Value,
                localFrame);
        }

        private static DamageProfileUsage Usage(
            int sourceEntityId,
            int targetEntityId,
            HitResolveResult hit,
            int localFrame)
        {
            return new DamageProfileUsage(
                DamageProfileSourceKind.WeaponTrace,
                new CharacterActionId(hit.ActionId),
                hit.ActionInstanceId,
                FixtureStableEventId,
                FixtureEquipmentSlotId,
                FixtureWeaponId,
                sourceEntityId,
                targetEntityId,
                CharacterTestAuthoredContentIds.D400_Id56,
                CharacterTestAuthoredContentIds.D400_Id3,
                hit.Frame.Value,
                localFrame);
        }

        private static CombatEntityId ToCombatEntityId(CharacterTestCharacterEntity entity)
        {
            return new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value);
        }

        private static CharacterTestBodyPartAcceptedDamageFact AcceptedDamage(
            long frame,
            int finalDamage)
        {
            return new CharacterTestBodyPartAcceptedDamageFact(
                authorityFrame: frame,
                sourceEntityId: 1,
                targetEntityId: TargetEntityId,
                actionInstanceId: 1,
                policyEventId: 1,
                traceId: 1,
                localFrame: 1,
                definitionIndex: 0,
                finalDamage: finalDamage);
        }

        private static int ResolveAcceptedBridgeFact(
            CharacterTestBodyPartDamageSourcePolicy sourcePolicy,
            CharacterTestBodyPartHitProvenance provenance,
            int frame,
            string bodyPartId = "head")
        {
            CharacterTestBodyPartStateComponent state = CreateReadyState(
                woundCapacity: 100,
                openDamageScaleMilli: 1_250,
                bodyPartId);
            var bridge = new CharacterTestBodyPartDamageAuthorityBridge();
            var hit = new HitResolveResult(
                new CombatEntityId(101),
                new CombatEntityId(107),
                actionId: 11,
                actionInstanceId: 22,
                traceId: 33,
                frame: new CombatFrame(frame),
                HitResolveKind.Damage,
                damage: 5,
                staggerFrames: 0,
                knockback: default);
            DamageProfileResolveResult payload = Damage(
                5,
                new DamageProfileUsage(
                    DamageProfileSourceKind.WeaponTrace,
                    new CharacterActionId(hit.ActionId),
                    hit.ActionInstanceId,
                    FixtureStableEventId,
                    FixtureEquipmentSlotId,
                    FixtureWeaponId,
                    sourceEntityId: 1,
                    targetEntityId: TargetEntityId,
                    hitZoneId: CharacterTestAuthoredContentIds.D400_Id56,
                    bodyPartId: CharacterTestAuthoredContentIds.D400_Id3,
                    frame: hit.Frame.Value,
                    localFrame: 4));
            var evidence = new CharacterTestBodyPartHitEvidence(
                ExplicitEvidence(bodyPartId).HitZone,
                provenance);

            Assert.IsTrue(bridge.BeginAuthorityFrame(frame));
            DamageProfileResolveResult resolved = bridge.ResolvePreHitDamage(
                frame,
                state,
                evidence,
                sourcePolicy,
                payload,
                out CharacterTestBodyPartPreHitDecision decision);
            Assert.IsTrue(bridge.RecordResolvedHit(
                new CharacterTestResolvedHitKey(hit),
                state,
                evidence,
                sourcePolicy,
                sourceEntityId: 1,
                targetEntityId: TargetEntityId,
                policyEventId: 44,
                localFrame: 4,
                decision));
            Assert.IsTrue(bridge.CacheResolvedDamageEvent(new DamageResolvedEvent(
                new DamageRequest(resolved.DamageProfileId, resolved.Usage),
                resolved,
                hit,
                finalDamage: 5)));
            Assert.IsTrue(bridge.CompleteAuthorityFrame(frame));
            Assert.IsTrue(state.TryGetCurrentEntry(0, out CharacterTestBodyPartStateEntry after));
            return after.WoundValue;
        }

        private static CharacterTestBodyPartDamageTelemetryProjection ResolveBridgeOutcome(
            HitResolveKind hitKind,
            int hitDamage,
            int finalDamage,
            int frame)
        {
            CharacterTestBodyPartStateComponent state = CreateReadyState(
                woundCapacity: 100,
                openDamageScaleMilli: 1_250);
            var bridge = new CharacterTestBodyPartDamageAuthorityBridge();
            var hit = new HitResolveResult(
                new CombatEntityId(101),
                new CombatEntityId(107),
                actionId: 11,
                actionInstanceId: 22,
                traceId: 33,
                frame: new CombatFrame(frame),
                hitKind,
                hitDamage,
                staggerFrames: 0,
                knockback: default);
            DamageProfileResolveResult payload = Damage(
                5,
                Usage(
                    sourceEntityId: 1,
                    targetEntityId: TargetEntityId,
                    hit,
                    localFrame: 4));

            Assert.IsTrue(bridge.BeginAuthorityFrame(frame));
            DamageProfileResolveResult resolved = bridge.ResolvePreHitDamage(
                frame,
                state,
                ExplicitEvidence("head"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                payload,
                out CharacterTestBodyPartPreHitDecision decision);
            Assert.IsTrue(bridge.RecordResolvedHit(
                new CharacterTestResolvedHitKey(hit),
                state,
                ExplicitEvidence("head"),
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                sourceEntityId: 1,
                targetEntityId: TargetEntityId,
                policyEventId: 44,
                localFrame: 4,
                decision));
            if (hitKind == HitResolveKind.Damage || hitKind == HitResolveKind.Blocked)
            {
                Assert.IsTrue(bridge.CacheResolvedDamageEvent(new DamageResolvedEvent(
                    new DamageRequest(resolved.DamageProfileId, resolved.Usage),
                    resolved,
                    hit,
                    finalDamage)));
            }
            else
            {
                Assert.IsTrue(bridge.CacheResolvedHitOutcome(hit));
            }
            Assert.IsTrue(bridge.CompleteAuthorityFrame(frame));
            Assert.AreEqual(1, bridge.DamageTelemetryCount);
            return bridge.GetDamageTelemetryAt(0);
        }

        private static bool RunAcceptedDamageFrame(
            CharacterTestBodyPartDamageAuthorityBridge bridge,
            CharacterTestBodyPartStateComponent state,
            CharacterTestBodyPartHitEvidence evidence,
            int frame)
        {
            var hit = new HitResolveResult(
                new CombatEntityId(101),
                new CombatEntityId(107),
                actionId: 11,
                actionInstanceId: 22,
                traceId: 33,
                frame: new CombatFrame(frame),
                HitResolveKind.Damage,
                damage: 1,
                staggerFrames: 0,
                knockback: default);
            DamageProfileResolveResult payload = Damage(
                1,
                Usage(
                    sourceEntityId: 1,
                    targetEntityId: TargetEntityId,
                    hit,
                    localFrame: 4));
            DamageProfileResolveResult resolved = bridge.ResolvePreHitDamage(
                frame,
                state,
                evidence,
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                payload,
                out CharacterTestBodyPartPreHitDecision decision);
            bool recorded = bridge.RecordResolvedHit(
                new CharacterTestResolvedHitKey(hit),
                state,
                evidence,
                CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                sourceEntityId: 1,
                targetEntityId: TargetEntityId,
                policyEventId: 44,
                localFrame: 4,
                decision);
            bool outcomeCached = bridge.CacheResolvedHitOutcome(hit);
            bool cached = bridge.CacheResolvedDamageEvent(new DamageResolvedEvent(
                new DamageRequest(resolved.DamageProfileId, resolved.Usage),
                resolved,
                hit,
                finalDamage: 1));
            bool completed = bridge.CompleteAuthorityFrame(frame);
            return recorded && outcomeCached && cached && completed;
        }
    }
}
