using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestBodyPartDamageZoneSelectionTests
    {
        private const int FixturePartAId = 1101;
        private const int FixturePartBId = 1102;
        private const int FixtureAlphaHitZoneId = 1201;
        private const int FixtureBetaHitZoneId = 1202;
        private const int FixtureAlphaHurtboxId = 1301;
        private const int FixtureBetaHurtboxId = 1302;
        private const int FixtureAlphaZoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0089;
        private const int FixtureBetaZoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0091;
        private const int FixtureReactionGroupId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0026;
        [Test]
        public void Projection_AggregatesMultiEntryWithBoundedSaturatingPriority_AndPreservesEligibility()
        {
            Fixture fixture = CreateFixture(alphaPriority: int.MaxValue - 5);
            Initialize(
                fixture.Target,
                Definition(0, "part.a", "member.a", "zone.alpha", 1, 1, 900, 700, "action.focus"),
                Definition(1, "part.b", "member.b", "zone.alpha", 1, 1, 500, 400, string.Empty));
            CommitDamage(fixture.Target, 0, 0, 1);
            CommitDamage(fixture.Target, 1, 1, 1);

            CharacterTestBodyPartDamageZoneSelectionState state = fixture.Project("zone.alpha");

            Assert.AreEqual(CharacterTestDamageZoneDynamicStateStatus.Ready, state.Status);
            Assert.AreEqual(2, state.MatchedDefinitionCount);
            Assert.AreEqual(2, state.OpenDefinitionCount);
            Assert.AreEqual(2, state.BrokenDefinitionCount);
            Assert.AreEqual(1_000, state.DynamicPriorityDelta, "same-Zone entries aggregate then clamp once.");
            Assert.AreEqual(int.MaxValue, state.EffectivePriority, "effective priority saturates without overflow.");
            Assert.IsTrue(state.FocusVisible, "only the Open definition with an explicit tag exposes Focus.");
            Assert.IsTrue(state.Hittable);
            Assert.IsFalse(state.Aimable);
            Assert.IsFalse(state.Approachable);

            Fixture mirrored = CreateFixture(alphaPriority: int.MaxValue - 5);
            Initialize(
                mirrored.Target,
                Definition(0, "part.a", "member.a", "zone.alpha", 1, 1, 500, 400, "action.focus"),
                Definition(1, "part.b", "member.b", "zone.alpha", 1, 1, 900, 700, string.Empty));
            CommitDamage(mirrored.Target, 0, 0, 1);
            CommitDamage(mirrored.Target, 1, 1, 1);
            Assert.AreEqual(
                state.DynamicPriorityDelta,
                mirrored.Project("zone.alpha").DynamicPriorityDelta,
                "same-Zone aggregation is independent of which stable definition carries each delta.");
        }

        [Test]
        public void Projection_FocusRequiresOpenTaggedWound_ConsumedAndBrokenOnlyStayHidden()
        {
            Fixture tagged = CreateFixture();
            Initialize(
                tagged.Target,
                Definition(0, "part.a", "member.a", "zone.alpha", 1, 100, 20, 30, "action.focus"));
            Assert.IsFalse(tagged.Project("zone.alpha").FocusVisible);
            CommitDamage(tagged.Target, 0, 0, 1);
            Assert.IsTrue(tagged.Project("zone.alpha").FocusVisible);
            CommitFocus(tagged.Target, 1, 0, "action.focus");
            CharacterTestBodyPartDamageZoneSelectionState consumed = tagged.Project("zone.alpha");
            Assert.AreEqual(1, consumed.ConsumedDefinitionCount);
            Assert.IsFalse(consumed.FocusVisible);

            Fixture brokenOnly = CreateFixture();
            Initialize(
                brokenOnly.Target,
                Definition(0, "part.a", "member.a", "zone.alpha", 100, 1, 20, 30, "action.focus"));
            CommitDamage(brokenOnly.Target, 0, 0, 1);
            CharacterTestBodyPartDamageZoneSelectionState broken = brokenOnly.Project("zone.alpha");
            Assert.AreEqual(0, broken.OpenDefinitionCount);
            Assert.AreEqual(1, broken.BrokenDefinitionCount);
            Assert.IsFalse(broken.FocusVisible);
            Assert.AreEqual(30, broken.DynamicPriorityDelta);

            Fixture noTag = CreateFixture();
            Initialize(
                noTag.Target,
                Definition(0, "part.a", "member.a", "zone.alpha", 1, 100, 20, 30, string.Empty));
            CommitDamage(noTag.Target, 0, 0, 1);
            Assert.IsFalse(noTag.Project("zone.alpha").FocusVisible);
        }

        [Test]
        public void Selector_EffectivePriorityIsStableUnderZoneOrderPerturbation()
        {
            Fixture fixture = CreateFixture(alphaPriority: 1, betaPriority: 9);
            Initialize(
                fixture.Target,
                Definition(0, "part.a", "member.a", "zone.alpha", 1, 100, 50, 0, string.Empty));
            CommitDamage(fixture.Target, 0, 0, 1);
            fixture.Evaluator.Set("zone.alpha", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.alpha");
            fixture.Evaluator.Set("zone.beta", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.beta");

            CharacterTestDamageZoneSelectionResult canonical = fixture.Select(default);
            fixture.RebuildZones(reverseOrder: true);
            CharacterTestDamageZoneSelectionResult reversed = fixture.Select(default);

            Assert.AreEqual(FixtureAlphaZoneId, canonical.Zone.ZoneId);
            Assert.AreEqual(canonical.Zone.ZoneId, reversed.Zone.ZoneId);
            Assert.AreEqual(50, canonical.DynamicState.DynamicPriorityDelta);
            Assert.AreEqual(51, canonical.DynamicState.EffectivePriority);
            Assert.IsFalse(canonical.DynamicState.Approachable, "dynamic priority cannot promote approach eligibility.");
            Assert.AreEqual(CharacterTestAttackOpportunityLevel.CurrentPose, canonical.Opportunity.Level);
            Assert.AreEqual(5, canonical.Opportunity.EarliestCandidateHitTick);
        }

        [Test]
        public void RevisionChange_RevalidatesAndKeepsExistingHysteresis()
        {
            Fixture fixture = CreateFixture(alphaPriority: 1, betaPriority: 9);
            Initialize(
                fixture.Target,
                Definition(0, "part.a", "member.a", "zone.alpha", 1, 100, 100, 0, string.Empty));
            fixture.Evaluator.Set("zone.alpha", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.alpha");
            fixture.Evaluator.Set("zone.beta", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.beta");
            CharacterTestDamageZoneLock previous = fixture.Select(default).ZoneLock;
            Assert.AreEqual(FixtureBetaZoneId, previous.ZoneId);

            CommitDamage(fixture.Target, 0, 0, 1);
            CharacterTestDamageZoneSelectionResult result = fixture.Select(previous);

            Assert.AreEqual(FixtureBetaZoneId, result.Zone.ZoneId, "priority-only change does not bypass the frozen lock hysteresis.");
            Assert.AreEqual(CharacterTestDamageZoneSwitchReason.PreviousLockKept, result.SwitchReason);
            Assert.AreEqual(
                CharacterTestDamageZoneStateRevisionReason.RevisionChangedLockKept,
                result.StateRevisionReason);
            Assert.AreEqual(fixture.Target.BodyPartState.Revision, result.ZoneLock.BodyPartStateRevision);
        }

        [Test]
        public void RevisionChange_SwitchesOnlyThroughExistingOpportunityRule()
        {
            Fixture fixture = CreateFixture(alphaPriority: 1, betaPriority: 9);
            Initialize(
                fixture.Target,
                Definition(0, "part.a", "member.a", "zone.alpha", 1, 100, 100, 0, string.Empty));
            fixture.Evaluator.Set("zone.alpha", CharacterTestAttackOpportunityLevel.TurnInPlace, 5, "hurt.alpha");
            fixture.Evaluator.Set("zone.beta", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.beta");
            CharacterTestDamageZoneLock previous = fixture.Select(default).ZoneLock;
            CommitDamage(fixture.Target, 0, 0, 1);
            fixture.Evaluator.Set("zone.alpha", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.alpha");
            fixture.Evaluator.Set("zone.beta", CharacterTestAttackOpportunityLevel.TurnInPlace, 5, "hurt.beta");

            CharacterTestDamageZoneSelectionResult result = fixture.Select(previous);

            Assert.AreEqual(FixtureAlphaZoneId, result.Zone.ZoneId);
            Assert.AreEqual(CharacterTestDamageZoneSwitchReason.OpportunityLevelImproved, result.SwitchReason);
            Assert.AreEqual(
                CharacterTestDamageZoneStateRevisionReason.RevisionChangedSelectionChanged,
                result.StateRevisionReason);
        }

        [Test]
        public void LegacyAndClosedIntactKeepAuthoredResult_InvalidStateFailsClosed()
        {
            Fixture legacy = CreateFixture(alphaPriority: 1, betaPriority: 9);
            legacy.Evaluator.Set("zone.alpha", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.alpha");
            legacy.Evaluator.Set("zone.beta", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.beta");
            CharacterTestDamageZoneSelectionResult legacyResult = legacy.Select(default);
            Assert.AreEqual(FixtureBetaZoneId, legacyResult.Zone.ZoneId);
            Assert.AreEqual(0, legacyResult.DynamicState.DynamicPriorityDelta);
            Assert.AreEqual(CharacterTestDamageZoneStateRevisionReason.LegacyNoDefinitions, legacyResult.StateRevisionReason);

            Fixture closed = CreateFixture(alphaPriority: 1, betaPriority: 9);
            Initialize(
                closed.Target,
                Definition(0, "part.a", "member.a", "zone.alpha", 1, 1, 100, 100, string.Empty));
            closed.Evaluator.Set("zone.alpha", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.alpha");
            closed.Evaluator.Set("zone.beta", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.beta");
            CharacterTestDamageZoneSelectionResult closedResult = closed.Select(default);
            Assert.AreEqual(FixtureBetaZoneId, closedResult.Zone.ZoneId);
            Assert.AreEqual(0, closedResult.DynamicState.DynamicPriorityDelta);

            Fixture invalid = CreateFixture();
            invalid.Target.BodyPartState.Reinitialize(CreateBinding(Array.Empty<CharacterBodyPartDamageRuntimeBinding>()));
            invalid.Evaluator.Set("zone.alpha", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.alpha");
            Assert.AreEqual(
                CharacterTestDamageZoneSelectionStatus.InvalidDynamicState,
                invalid.Select(default).Status);
        }

        [Test]
        public void JointProjection_FeedsGeneratorSeedWithSelectorEffectivePriorityAndEligibility()
        {
            Fixture fixture = CreateFixture(alphaPriority: 1, betaPriority: 9);
            Initialize(
                fixture.Target,
                Definition(0, "part.a", "member.a", "zone.alpha", 1, 100, 50, 0, string.Empty));
            CommitDamage(fixture.Target, 0, 0, 1);
            fixture.Evaluator.Set("zone.alpha", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.alpha");
            fixture.Evaluator.Set("zone.beta", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.beta");
            CharacterTestDamageZoneSelectionResult selected = fixture.Select(default);
            var plannerAdapter = new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(null);
            CharacterTestDamageZoneAggregate alpha = fixture.GetZone("zone.alpha");
            Assert.IsTrue(plannerAdapter.TryProjectJointZoneState(
                fixture.Target,
                fixture.Zones,
                alpha,
                out CharacterTestBodyPartDamageZoneSelectionState jointState));

            var generator = new CharacterTestRuntimeAiJointCandidateGenerator();
            CharacterTestRuntimeAiJointActorInput[] actors =
            {
                new CharacterTestRuntimeAiJointActorInput(
                    fixture.Target.Id.Value,
                    2,
                    CharacterTestCharacterLifeState.Alive,
                    snapshotCurrent: true,
                    rootSpatialDistanceMillimetersSquared: 9,
                    maskCompatible: true,
                    withinSoftSearchRadius: true)
            };
            CharacterTestRuntimeAiJointActionInput[] actions =
            {
                new CharacterTestRuntimeAiJointActionInput(
                    1,
                    new CharacterActionId(101),
                    CharacterTestRuntimeAiJointCandidateSource.EvaluatorBacked,
                    ready: true,
                    resolvable: true,
                    actionUtilityMilli: 1_000,
                    new CharacterTestRuntimeAiJointAabb(0, 0, 0, 10, 10, 10))
            };
            CharacterTestRuntimeAiJointZoneInput[] zones =
            {
                new CharacterTestRuntimeAiJointZoneInput(
                    fixture.Target.Id.Value,
                    alpha.ZoneId,
                    jointState.Hittable,
                    jointState.Approachable,
                    jointState.EffectivePriority,
                    new CharacterTestRuntimeAiJointAabb(0, 0, 0, 10, 10, 10))
            };
            generator.Generate(1, 1, actors, 1, actions, 1, zones, 1,
                new CharacterTestRuntimeAiJointGenerationBudget());

            Assert.AreEqual(selected.DynamicState.EffectivePriority, jointState.EffectivePriority);
            Assert.AreEqual(jointState.EffectivePriority, generator.GetCandidate(0).ZonePriority);
            Assert.IsFalse(generator.GetCandidate(0).ZoneApproachable);
        }

        [Test]
        public void HittableFalse_RemainsExcludedFromSelectorAndJointProjection()
        {
            Fixture fixture = CreateFixture(alphaHittable: false, alphaPriority: 1, betaPriority: 9);
            Initialize(
                fixture.Target,
                Definition(0, "part.a", "member.a", "zone.alpha", 1, 1, 1_000, 1_000, "action.focus"));
            CommitDamage(fixture.Target, 0, 0, 1);
            fixture.Evaluator.Set("zone.alpha", CharacterTestAttackOpportunityLevel.CurrentPose, 1, "hurt.alpha");
            fixture.Evaluator.Set("zone.beta", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.beta");

            CharacterTestDamageZoneSelectionResult selected = fixture.Select(default);
            var plannerAdapter = new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(null);
            Assert.IsTrue(plannerAdapter.TryProjectJointZoneState(
                fixture.Target,
                fixture.Zones,
                fixture.GetZone("zone.alpha"),
                out CharacterTestBodyPartDamageZoneSelectionState state));

            Assert.AreEqual(FixtureBetaZoneId, selected.Zone.ZoneId);
            Assert.IsFalse(state.Hittable);
            Assert.IsFalse(state.Approachable);
        }

        [Test]
        public void SelectionAndJointProjection_Warmed4096IterationsAllocateZeroBytes()
        {
            Fixture fixture = CreateFixture(alphaPriority: 1, betaPriority: 9);
            Initialize(
                fixture.Target,
                Definition(0, "part.a", "member.a", "zone.alpha", 1, 100, 50, 0, string.Empty));
            CommitDamage(fixture.Target, 0, 0, 1);
            fixture.Evaluator.Set("zone.alpha", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.alpha");
            fixture.Evaluator.Set("zone.beta", CharacterTestAttackOpportunityLevel.CurrentPose, 5, "hurt.beta");
            var plannerAdapter = new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(null);
            CharacterTestDamageZoneAggregate alpha = fixture.GetZone("zone.alpha");
            CharacterTestDamageZoneLock previous = fixture.Select(default).ZoneLock;
            plannerAdapter.TryProjectJointZoneState(fixture.Target, fixture.Zones, alpha, out _);
            var generator = new CharacterTestRuntimeAiJointCandidateGenerator();
            CharacterTestRuntimeAiJointActorInput[] actors =
            {
                new CharacterTestRuntimeAiJointActorInput(
                    fixture.Target.Id.Value, 2, CharacterTestCharacterLifeState.Alive,
                    snapshotCurrent: true, rootSpatialDistanceMillimetersSquared: 9)
            };
            CharacterTestRuntimeAiJointActionInput[] actions =
            {
                new CharacterTestRuntimeAiJointActionInput(
                    1, new CharacterActionId(101),
                    CharacterTestRuntimeAiJointCandidateSource.EvaluatorBacked,
                    ready: true, resolvable: true, actionUtilityMilli: 1_000,
                    new CharacterTestRuntimeAiJointAabb(0, 0, 0, 10, 10, 10))
            };
            var zoneInputs = new CharacterTestRuntimeAiJointZoneInput[1];
            var budget = new CharacterTestRuntimeAiJointGenerationBudget();
            zoneInputs[0] = new CharacterTestRuntimeAiJointZoneInput(
                fixture.Target.Id.Value, alpha.ZoneId, true, false, 51,
                new CharacterTestRuntimeAiJointAabb(0, 0, 0, 10, 10, 10));
            generator.Generate(1, 1, actors, 1, actions, 1, zoneInputs, 1, budget);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            CharacterTestBodyPartDamageZoneSelectionState projection = default;
            for (int i = 0; i < 4096; i++)
            {
                previous = fixture.Select(previous).ZoneLock;
                plannerAdapter.TryProjectJointZoneState(
                    fixture.Target,
                    fixture.Zones,
                    alpha,
                    out projection);
                zoneInputs[0] = new CharacterTestRuntimeAiJointZoneInput(
                    fixture.Target.Id.Value,
                    alpha.ZoneId,
                    projection.Hittable,
                    projection.Approachable,
                    projection.EffectivePriority,
                    zoneInputs[0].WorldBounds);
                generator.Generate(1, 1, actors, 1, actions, 1, zoneInputs, 1, budget);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(51, projection.EffectivePriority);
            Assert.AreEqual(51, generator.GetCandidate(0).ZonePriority);
        }

        [Test]
        public void EightCharacters_StatePreHitDamageAndSelector_Warmed4096IterationsAllocateZeroBytes()
        {
            const int CharacterCount = 8;
            const int WarmupIterations = 32;
            const int MeasuredIterations = 4096;
            var fixtures = new Fixture[CharacterCount];
            var locks = new CharacterTestDamageZoneLock[CharacterCount];
            var bridges = new CharacterTestBodyPartDamageAuthorityBridge[CharacterCount];
            var sharedWorld = new CombatPhysicsWorld();
            CharacterTestBodyPartHitEvidence evidence = ExplicitEvidence("member.a");
            DamageProfileResolveResult baseDamage = SteadyDamage();
            for (int i = 0; i < CharacterCount; i++)
            {
                Fixture fixture = CreateFixture(
                    alphaPriority: 1,
                    betaPriority: 9,
                    physicsWorld: sharedWorld,
                    targetEntityId: i + 2);
                Initialize(
                    fixture.Target,
                    Definition(
                        0,
                        "part.a",
                        "member.a",
                        "zone.alpha",
                        woundCapacity: 1_000_000,
                        maxIntegrity: 1_000_000,
                        openDelta: 50,
                        brokenDelta: 0,
                        focusTag: string.Empty));
                fixture.Evaluator.Set(
                    "zone.alpha",
                    CharacterTestAttackOpportunityLevel.CurrentPose,
                    5,
                    "hurt.alpha");
                fixture.Evaluator.Set(
                    "zone.beta",
                    CharacterTestAttackOpportunityLevel.CurrentPose,
                    5,
                    "hurt.beta");
                fixtures[i] = fixture;
                locks[i] = fixture.Select(default).ZoneLock;
                bridges[i] = new CharacterTestBodyPartDamageAuthorityBridge();
            }

            bool succeeded = true;
            for (int i = 0; i < WarmupIterations; i++)
            {
                succeeded &= RunStateDamageSelectorStep(
                    fixtures,
                    locks,
                    bridges,
                    evidence,
                    baseDamage,
                    i + 1);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < MeasuredIterations; i++)
            {
                succeeded &= RunStateDamageSelectorStep(
                    fixtures,
                    locks,
                    bridges,
                    evidence,
                    baseDamage,
                    WarmupIterations + i + 1);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(succeeded);
            Assert.AreEqual(0L, allocated);
            for (int i = 0; i < CharacterCount; i++)
            {
                Assert.IsTrue(fixtures[i].Target.BodyPartState.TryGetCurrentEntry(
                    0,
                    out CharacterTestBodyPartStateEntry entry));
                Assert.AreEqual(WarmupIterations + MeasuredIterations, entry.WoundValue);
                Assert.AreEqual(FixtureBetaZoneId, locks[i].ZoneId);
                Assert.AreEqual(i + 2, fixtures[i].Target.Id.Value);
            }
        }

        private static bool RunStateDamageSelectorStep(
            Fixture[] fixtures,
            CharacterTestDamageZoneLock[] locks,
            CharacterTestBodyPartDamageAuthorityBridge[] bridges,
            CharacterTestBodyPartHitEvidence evidence,
            DamageProfileResolveResult baseDamage,
            int authorityFrame)
        {
            for (int i = 0; i < fixtures.Length; i++)
            {
                CharacterTestBodyPartStateComponent state = fixtures[i].Target.BodyPartState;
                CharacterTestBodyPartDamageAuthorityBridge bridge = bridges[i];
                if (!bridge.BeginAuthorityFrame(authorityFrame))
                    return false;
                DamageProfileResolveResult resolved = bridge.ResolvePreHitDamage(
                    authorityFrame,
                    state,
                    evidence,
                    CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                    baseDamage,
                    out CharacterTestBodyPartPreHitDecision decision);
                if (decision.IgnoredReason != CharacterTestBodyPartDamageIgnoredReason.None
                    || resolved.IncomingDamage != 1)
                {
                    return false;
                }
                if (state.StageAcceptedDamage(
                    new CharacterTestBodyPartAcceptedDamageFact(
                        authorityFrame,
                        sourceEntityId: 1,
                        targetEntityId: state.OwnerEntityId,
                        actionInstanceId: authorityFrame,
                        policyEventId: i + 1,
                        traceId: i + 1,
                        localFrame: i,
                        definitionIndex: 0,
                        finalDamage: resolved.IncomingDamage))
                    != CharacterTestBodyPartStateStageResult.Staged)
                {
                    return false;
                }
                if (!bridge.CompleteAuthorityFrame(authorityFrame))
                    return false;
                CharacterTestDamageZoneSelectionResult selected = fixtures[i].Select(locks[i]);
                if (!selected.IsUsable)
                    return false;
                locks[i] = selected.ZoneLock;
            }
            return true;
        }

        private static DamageProfileResolveResult SteadyDamage()
        {
            return new DamageProfileResolveResult(
                success: true,
                reason: string.Empty,
                damageProfileId: 0,
                damageTypeId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D409_Id2,
                formulaId: DamageFormulaIds.Flat,
                incomingDamage: 1,
                staggerFrames: 0,
                impactForce: 0,
                knockback: default,
                impactPower: 0,
                guardPressure: 0,
                presentationProfileId: 0,
                usage: default);
        }

        private static CharacterTestBodyPartHitEvidence ExplicitEvidence(string bodyPartId)
        {
            int bodyPartNumericId = ToFixtureId(bodyPartId);
            return new CharacterTestBodyPartHitEvidence(
                new CharacterTestHitZoneResolution(
                    bodyPartNumericId,
                    bodyPartNumericId,
                    FixtureReactionGroupId,
                    damageMultiplier: 1f,
                    staggerScale: 1f,
                    impulseScale: 1f,
                    impactPowerScale: 1f),
                CharacterTestBodyPartHitProvenance.ExplicitColliderBinding);
        }

        private static Fixture CreateFixture(
            int alphaPriority = 1,
            int betaPriority = 9,
            bool alphaHittable = true,
            CombatPhysicsWorld physicsWorld = null,
            int targetEntityId = 2)
        {
            CombatPhysicsWorld world = physicsWorld ?? new CombatPhysicsWorld();
            RuntimeFrame frame = new RuntimeFrame(11);
            CharacterTestCharacterEntity source = CreateEntity(1, teamId: 1);
            CharacterTestCharacterEntity target = CreateEntity(targetEntityId, teamId: 2);
            var geometry = new CharacterTestDamageGeometrySnapshot(2);
            geometry.BeginWrite(
                targetEntityId,
                target.GameplayEntityId,
                new CombatEntityId(
                    CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + targetEntityId));
            int colliderIdBase = (targetEntityId - 2) * 2;
            AddCollider(geometry, "hurt.alpha", colliderIdBase + 1, "hit.alpha");
            AddCollider(geometry, "hurt.beta", colliderIdBase + 2, "hit.beta");
            geometry.SortCollidersByStableLogicalId();
            geometry.Commit(frame, world.Revision, 7);
            Assert.IsTrue(CharacterTestAttackEvaluationSnapshot.TryCapture(
                frame,
                world,
                source,
                target,
                geometry,
                3,
                5,
                13,
                out CharacterTestAttackEvaluationSnapshot snapshot));
            var query = new CharacterTestAttackOpportunityQuery(
                snapshot,
                geometry,
                world,
                source,
                target,
                default,
                false,
                default,
                false,
                frame,
                world.Revision,
                3,
                5,
                geometry.HurtboxRevision,
                13,
                null,
                null);
            return new Fixture(
                query,
                geometry,
                source,
                target,
                new FakeEvaluator(),
                alphaPriority,
                betaPriority,
                alphaHittable);
        }

        private static CharacterTestCharacterEntity CreateEntity(int id, int teamId)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(id),
                new CharacterTestCharacterIdentityComponent(
                    1,
                    "M2-T4",
                    default(ResourceKey),
                    default(CharacterRuntimeAnimationBinding)),
                new CharacterTestCharacterTeamComponent(teamId, teamId),
                new CharacterTestCharacterTransformComponent(id == 1 ? 0f : 3f, 0f, 0f, 0f),
                new CharacterTestCharacterEquipmentComponent(false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                new GameplayEntityId(id, 1),
                CharacterTestCharacterPhysicsComponent.Default);
        }

        private static void Initialize(
            CharacterTestCharacterEntity target,
            params CharacterBodyPartDamageRuntimeBinding[] definitions)
        {
            Assert.AreEqual(
                CharacterTestBodyPartStateInitializationStatus.Ready,
                target.BodyPartState.Reinitialize(CreateBinding(definitions)));
        }

        private static CharacterImportedGeometryBinding CreateBinding(
            CharacterBodyPartDamageRuntimeBinding[] definitions)
        {
            var lookup = new CharacterBodyPartDamageLookupRuntimeBinding[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                lookup[i] = new CharacterBodyPartDamageLookupRuntimeBinding(
                    definitions[i].MemberPartIds[0],
                    i);
            }
            Array.Sort(lookup, (left, right) => left.PartId.CompareTo(right.PartId));
            return new CharacterImportedGeometryBinding(
                format: "mx.characterGeometryBinding.v5",
                schemaVersion: "5.0",
                packageId: "test.body-part-zone-selection",
                bodyProfile: null,
                bodyColliders: Array.Empty<CharacterBodyColliderRuntimeBinding>(),
                hitZoneBindings: Array.Empty<CharacterHitZoneRuntimeBinding>(),
                damageZones: Array.Empty<CharacterDamageZoneRuntimeBinding>(),
                sockets: Array.Empty<CharacterSocketRuntimeBinding>(),
                weaponAttachments: Array.Empty<CharacterWeaponAttachmentRuntimeBinding>(),
                weaponTraces: Array.Empty<CharacterWeaponTraceRuntimeBinding>(),
                bodyPartDamageDefinitions: definitions,
                bodyPartDamagePartLookup: lookup,
                bodyPartDamageFormat: "mx.characterBodyPartDamage.v1",
                bodyPartDamageSchemaVersion: "1.0",
                bodyPartDamageRevision: 1,
                bodyPartDamageContentHash: "sha256:test",
                bodyPartDamageMode: CharacterBodyPartDamageMode.Authored);
        }

        private static CharacterBodyPartDamageRuntimeBinding Definition(
            int index,
            string partStateId,
            string memberPartId,
            string zoneId,
            int woundCapacity,
            int maxIntegrity,
            int openDelta,
            int brokenDelta,
            string focusTag)
        {
            return new CharacterBodyPartDamageRuntimeBinding(
                index,
                ToFixtureId(partStateId),
                new[] { ToFixtureId(memberPartId) },
                ToZoneId(zoneId),
                woundCapacity,
                woundCapacity > 0 ? 1_250 : 0,
                focusTag,
                maxIntegrity,
                maxIntegrity > 0 ? 1_000 : 0,
                maxIntegrity > 0 ? "reaction.break" : string.Empty,
                openDelta,
                brokenDelta,
                partStateLabel: partStateId,
                damageZoneLabel: zoneId,
                memberPartLabels: new[] { memberPartId });
        }

        private static void CommitDamage(
            CharacterTestCharacterEntity target,
            long frame,
            int definitionIndex,
            int damage)
        {
            CharacterTestBodyPartStateComponent state = target.BodyPartState;
            Assert.IsTrue(state.TryBeginFrame(frame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageAcceptedDamage(new CharacterTestBodyPartAcceptedDamageFact(
                    frame,
                    sourceEntityId: 1,
                    targetEntityId: target.Id.Value,
                    actionInstanceId: (int)frame + 1,
                    policyEventId: definitionIndex + 1,
                    traceId: definitionIndex + 1,
                    localFrame: definitionIndex,
                    definitionIndex,
                    finalDamage: damage)));
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.Committed, state.CommitFrame().Status);
        }

        private static void CommitFocus(
            CharacterTestCharacterEntity target,
            long frame,
            int definitionIndex,
            string tag)
        {
            CharacterTestBodyPartStateComponent state = target.BodyPartState;
            Assert.IsTrue(state.TryBeginFrame(frame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageFocusConsume(new CharacterTestBodyPartFocusConsumeFact(
                    frame,
                    CharacterTestBodyPartFocusConsumeAdapterKind.Action,
                    sourceEntityId: 1,
                    targetEntityId: target.Id.Value,
                    definitionIndex,
                    tag,
                    correlationId: 1)));
            Assert.AreEqual(CharacterTestBodyPartStateCommitStatus.Committed, state.CommitFrame().Status);
        }

        private static void AddCollider(
            CharacterTestDamageGeometrySnapshot geometry,
            string id,
            int colliderId,
            string hitZoneId)
        {
            geometry.AddCollider(
                ToHurtboxId(id),
                id,
                new CombatColliderId(colliderId),
                ToFixtureId(id),
                id,
                ToHitZoneId(hitZoneId),
                hitZoneId,
                0,
                CombatPhysicsShape.Aabb(FixVector3.Zero, FixVector3.Zero),
                FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.One, Fix64.One));
        }

        private static int ToFixtureId(string value)
        {
            if (string.Equals(value, "part.a", StringComparison.Ordinal)
                || string.Equals(value, "member.a", StringComparison.Ordinal)
                || string.Equals(value, "hurt.alpha", StringComparison.Ordinal))
            {
                return FixturePartAId;
            }
            if (string.Equals(value, "part.b", StringComparison.Ordinal)
                || string.Equals(value, "member.b", StringComparison.Ordinal)
                || string.Equals(value, "hurt.beta", StringComparison.Ordinal))
            {
                return FixturePartBId;
            }
            return FixturePartAId;
        }

        private static int ToHurtboxId(string value)
        {
            return string.Equals(value, "hurt.beta", StringComparison.Ordinal)
                ? FixtureBetaHurtboxId
                : FixtureAlphaHurtboxId;
        }

        private static int ToHitZoneId(string value)
        {
            return string.Equals(value, "hit.beta", StringComparison.Ordinal)
                ? FixtureBetaHitZoneId
                : FixtureAlphaHitZoneId;
        }

        private static int ToZoneId(string value)
        {
            return string.Equals(value, "zone.beta", StringComparison.Ordinal)
                ? FixtureBetaZoneId
                : FixtureAlphaZoneId;
        }

        private sealed class Fixture
        {
            private readonly CharacterTestDamageZoneSelector _selector;
            private readonly int _alphaPriority;
            private readonly int _betaPriority;
            private readonly bool _alphaHittable;

            internal Fixture(
                CharacterTestAttackOpportunityQuery query,
                CharacterTestDamageGeometrySnapshot geometry,
                CharacterTestCharacterEntity source,
                CharacterTestCharacterEntity target,
                FakeEvaluator evaluator,
                int alphaPriority,
                int betaPriority,
                bool alphaHittable)
            {
                Query = query;
                Geometry = geometry;
                Source = source;
                Target = target;
                Evaluator = evaluator;
                _alphaPriority = alphaPriority;
                _betaPriority = betaPriority;
                _alphaHittable = alphaHittable;
                _selector = new CharacterTestDamageZoneSelector(evaluator);
                RebuildZones(reverseOrder: false);
            }

            internal CharacterTestAttackOpportunityQuery Query { get; }
            internal CharacterTestDamageGeometrySnapshot Geometry { get; }
            internal CharacterTestCharacterEntity Source { get; }
            internal CharacterTestCharacterEntity Target { get; }
            internal FakeEvaluator Evaluator { get; }
            internal CharacterTestDamageZoneSnapshot Zones { get; private set; }

            internal void RebuildZones(bool reverseOrder)
            {
                var alpha = new CharacterDamageZoneRuntimeBinding(
                    FixtureAlphaZoneId,
                    "Alpha",
                    new[] { FixtureAlphaHitZoneId },
                    _alphaHittable,
                    aimable: false,
                    approachable: false,
                    authoredPriority: _alphaPriority,
                    zoneLabel: "zone.alpha");
                var beta = new CharacterDamageZoneRuntimeBinding(
                    FixtureBetaZoneId,
                    "Beta",
                    new[] { FixtureBetaHitZoneId },
                    hittable: true,
                    aimable: true,
                    approachable: true,
                    authoredPriority: _betaPriority,
                    zoneLabel: "zone.beta");
                Zones = new CharacterTestDamageZoneSnapshot();
                Zones.Build(Geometry, reverseOrder ? new[] { beta, alpha } : new[] { alpha, beta });
            }

            internal CharacterTestDamageZoneAggregate GetZone(string zoneId)
            {
                Assert.IsTrue(Zones.TryGetZone(ToZoneId(zoneId), out CharacterTestDamageZoneAggregate zone));
                return zone;
            }

            internal CharacterTestBodyPartDamageZoneSelectionState Project(string zoneId)
            {
                var adapter = new CharacterTestBodyPartDamageZoneSelectionStateAdapter();
                Assert.IsTrue(adapter.TryProject(Target, Zones, GetZone(zoneId), out CharacterTestBodyPartDamageZoneSelectionState state));
                return state;
            }

            internal CharacterTestDamageZoneSelectionResult Select(CharacterTestDamageZoneLock previous)
            {
                return _selector.Select(Query, Zones, previous);
            }
        }

        private sealed class FakeEvaluator : ICharacterTestAttackOpportunityEvaluator
        {
            private readonly Dictionary<int, CharacterTestAttackOpportunityResult> _results =
                new Dictionary<int, CharacterTestAttackOpportunityResult>();

            internal void Set(
                string zoneId,
                CharacterTestAttackOpportunityLevel level,
                int tick,
                string hurtboxId)
            {
                int zoneNumericId = ToZoneId(zoneId);
                int hurtboxNumericId = ToHurtboxId(hurtboxId);
                _results[zoneNumericId] = new CharacterTestAttackOpportunityResult(
                    CharacterTestAttackOpportunityStatus.Valid,
                    CharacterTestAttackEvaluationMode.CurrentPose,
                    level,
                    hasCompatibleHurtbox: true,
                    envelopeIntersectsTarget: true,
                    traceCanResolveTargetHit: true,
                    canHitFromCurrentPose: level == CharacterTestAttackOpportunityLevel.CurrentPose,
                    canHitAfterTurnInPlace: level >= CharacterTestAttackOpportunityLevel.TurnInPlace,
                    requiresTranslation: level == CharacterTestAttackOpportunityLevel.RequiresTranslation,
                    requiredYawDeltaMilliDegrees: 0,
                    earliestCandidateHitTick: tick,
                    traceSampleIndex: 0,
                    stableSampleId: 1,
                    selectedHurtboxId: hurtboxNumericId,
                    selectedHitZoneId: hurtboxId == "hurt.alpha" ? FixtureAlphaHitZoneId : FixtureBetaHitZoneId,
                    selectedDamageZoneId: zoneNumericId,
                    witnessPoint: FixVector3.Zero,
                    aimPoint: FixVector3.Zero,
                    CharacterTestAttackOpportunityFailureReason.None,
                    CharacterTestAttackOpportunityFailureFlags.None);
            }

            public CharacterTestAttackOpportunityResult Evaluate(
                CharacterTestAttackOpportunityQuery query,
                int requiredYawDeltaMilliDegrees = 0)
            {
                return _results.TryGetValue(query.RequiredDamageZoneId, out CharacterTestAttackOpportunityResult result)
                    ? result
                    : default;
            }
        }
    }
}
