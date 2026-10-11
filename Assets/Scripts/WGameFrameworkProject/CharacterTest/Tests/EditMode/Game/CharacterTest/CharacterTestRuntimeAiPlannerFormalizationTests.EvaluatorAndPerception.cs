using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Physics;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerFormalizationTests
    {
        [Test]
        public void RuntimeAiPlannerEvaluator_RangedKiterRetreatsWhenTargetIsTooClose()
        {
            CharacterTestRuntimeAiPlannerProfile profile =
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateRangedKiterTemplate();
            using var harness = CreateHarness(profile, disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 1f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            CharacterTestRuntimeAiPlannerEvaluator evaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource(), profile);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision decision));

            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.Retreat, decision.SelectedActionKind);
            Assert.AreEqual("retreat", decision.ActiveStateId);
            Assert.IsTrue(decision.ShouldSubmitControlIntent);
        }

        [Test]
        public void RuntimeAiPlannerDecisionTrace_FirstRunningObservationIsRunningNotStarted()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 2f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            ai.SetAction(CharacterTestCharacterActionComponent.FromDeterministic(
                MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.preexisting-action"),
                MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running,
                MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Active,
                1,
                default,
                string.Empty,
                string.Empty,
                0,
                0,
                0));
            CharacterTestRuntimeAiPlannerEvaluator evaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource());

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out _));
            var store = new CharacterTestRuntimeAiLabDecisionTraceStore(1);
            CharacterTestRuntimeAiLabDecisionTraceWriter writer =
                store.BeginCapture(1L, 1, out var handle);
            evaluator.WriteDecisionTraces(writer, 1);
            writer.Seal();

            Assert.IsTrue(store.TryGetAgent(handle, ai.Id.Value, out var trace));
            Assert.AreEqual(
                CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Running,
                trace.Commit.RuntimeEffectKind);
            Assert.AreEqual("PLANNER_RUNTIME_ACTION_RUNNING", trace.Commit.RuntimeEffectReasonCode);
        }

        [Test]
        public void RuntimeAiPlannerEvaluator_HazardSkirmisherPrioritizesHazardRetreat()
        {
            CharacterTestRuntimeAiPlannerProfile profile =
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateHazardSkirmisherTemplate();
            using var harness = CreateHarness(profile, disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 2f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            var source = new FakeRuntimeAiPlannerPerceptionSource();
            source.Environment.NearHazard = true;
            CharacterTestRuntimeAiPlannerEvaluator evaluator = CreateEvaluator(source, profile);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision decision));

            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.Retreat, decision.SelectedActionKind);
            Assert.AreEqual("recover", decision.ActiveStateId);
            Assert.IsTrue(decision.EnvironmentNearHazard);
        }

        [Test]
        public void RuntimeAiPlannerEvaluator_AggressiveBerserkerAttacksInMeleeRange()
        {
            CharacterTestRuntimeAiPlannerProfile profile =
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateAggressiveBerserkerTemplate();
            using var harness = CreateHarness(profile, disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 1.2f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            CharacterTestRuntimeAiPlannerEvaluator evaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource(), profile);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision decision));

            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack, decision.SelectedActionKind);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerProfileSource.AttackStateId, decision.ActiveStateId);
            Assert.IsTrue(decision.AttackPrimaryPressed);
        }

        [Test]
        public void RuntimeAiPlannerEvaluator_DefensiveGuardianHoldsNearUsefulObject()
        {
            CharacterTestRuntimeAiPlannerProfile profile =
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateDefensiveGuardianTemplate();
            using var harness = CreateHarness(profile, disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 3f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            var source = new FakeRuntimeAiPlannerPerceptionSource();
            source.Environment.NearUsefulObject = true;
            CharacterTestRuntimeAiPlannerEvaluator evaluator = CreateEvaluator(source, profile);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision decision));

            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.Strafe, decision.SelectedActionKind);
            Assert.AreEqual("hold", decision.ActiveStateId);
            Assert.IsTrue(decision.EnvironmentNearUsefulObject);
        }

        [Test]
        public void RuntimeAiPlannerEvaluator_AmbusherCastsSlotWhenTargetEntersStrikeRange()
        {
            CharacterTestRuntimeAiPlannerProfile profile =
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateAmbusherTemplate();
            using var harness = CreateHarness(profile, disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 1.8f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            CharacterTestRuntimeAiPlannerEvaluator evaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource(), profile);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision decision));

            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot, decision.SelectedActionKind);
            Assert.AreEqual("strike", decision.ActiveStateId);
            Assert.AreEqual(CharacterTestSkillSlotRuntime.ToMask(1), decision.SkillSlotPressedMask);
        }

        [Test]
        public void RuntimeAiPlannerEvaluator_ControllerZonerEvadesWhenTargetIsTooClose()
        {
            CharacterTestRuntimeAiPlannerProfile profile =
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateControllerZonerTemplate();
            using var harness = CreateHarness(profile, disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 1f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            CharacterTestRuntimeAiPlannerEvaluator evaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource(), profile);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision decision));

            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.Retreat, decision.SelectedActionKind);
            Assert.AreEqual("evade", decision.ActiveStateId);
            Assert.IsTrue(decision.ShouldSubmitControlIntent);
        }

        [Test]
        public void RuntimeAiPlannerEvaluator_MoveToTargetAddsConfiguredLateralBlend()
        {
            CharacterTestRuntimeAiPlannerProfile profile = CreateSingleMovementActionProfile(
                CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                new CharacterTestRuntimeAiPlannerMovementSettings(
                    speedScale: 1f,
                    steeringResponsiveness: 1f,
                    approachLateralBlend: 0.5f,
                    arrivalSlowdownDistance: 0f,
                    directionHoldFrames: 0));
            using var harness = CreateHarness(profile, disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 6f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            CharacterTestRuntimeAiPlannerEvaluator evaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource(), profile);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision decision));

            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.MoveToTarget, decision.SelectedActionKind);
            Assert.Greater(decision.Move.x, 0f);
            Assert.Greater(Mathf.Abs(decision.Move.y), 0.01f);
        }

        [Test]
        public void RuntimeAiPlannerEvaluator_StrafeSwitchesSideDeterministically()
        {
            CharacterTestRuntimeAiPlannerProfile profile = CreateSingleMovementActionProfile(
                CharacterTestRuntimeAiPlannerActionKind.Strafe,
                new CharacterTestRuntimeAiPlannerMovementSettings(
                    speedScale: 1f,
                    steeringResponsiveness: 1f,
                    strafeSwitchMinFrames: 1,
                    strafeSwitchMaxFrames: 1,
                    directionHoldFrames: 0));
            using var harness = CreateHarness(profile, disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 6f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            CharacterTestRuntimeAiPlannerEvaluator evaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource(), profile);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision first));
            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(2),
                out CharacterTestRuntimeAiPlannerDecision second));

            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.Strafe, first.SelectedActionKind);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.Strafe, second.SelectedActionKind);
            Assert.AreEqual(-first.MovementStrafeSide, second.MovementStrafeSide);
            Assert.AreEqual(-Mathf.Sign(first.Move.y), Mathf.Sign(second.Move.y));
        }

        [Test]
        public void RuntimeAiPlannerEvaluator_RetreatAddsLateralBias()
        {
            CharacterTestRuntimeAiPlannerProfile profile = CreateSingleMovementActionProfile(
                CharacterTestRuntimeAiPlannerActionKind.Retreat,
                new CharacterTestRuntimeAiPlannerMovementSettings(
                    speedScale: 1f,
                    steeringResponsiveness: 1f,
                    approachLateralBlend: 0.35f,
                    directionHoldFrames: 0));
            using var harness = CreateHarness(profile, disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 6f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            CharacterTestRuntimeAiPlannerEvaluator evaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource(), profile);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision decision));

            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.Retreat, decision.SelectedActionKind);
            Assert.Less(decision.Move.x, 0f);
            Assert.Greater(Mathf.Abs(decision.Move.y), 0.01f);
        }

        [Test]
        public void RuntimeAiPlannerEvaluator_PreferredDistanceStopsMoveToTarget()
        {
            CharacterTestRuntimeAiPlannerProfile profile = CreateSingleMovementActionProfile(
                CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                new CharacterTestRuntimeAiPlannerMovementSettings(
                    speedScale: 1f,
                    steeringResponsiveness: 1f,
                    approachLateralBlend: 0.5f,
                    preferredDistanceMin: 2f,
                    preferredDistanceMax: 4f,
                    arrivalSlowdownDistance: 1f,
                    directionHoldFrames: 0));
            using var harness = CreateHarness(profile, disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 3f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            CharacterTestRuntimeAiPlannerEvaluator evaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource(), profile);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision decision));

            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.MoveToTarget, decision.SelectedActionKind);
            Assert.AreEqual(0f, decision.Move.magnitude, 0.001f);
            Assert.AreEqual(0f, decision.MovementSpeedScale, 0.001f);
        }

        [Test]
        public void RuntimeAiPlannerCommandModule_NewProfilesDoNotSubmitAttackWithoutTarget()
        {
            using var harness = CreateHarness(
                CharacterTestRuntimeAiPlannerTemplateLibrary.CreateSkillCasterTemplate(),
                disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));

            harness.Tick();

            CharacterTestRuntimeAiPlannerSnapshot snapshot =
                harness.Modules.RuntimeAiPlannerCommands.CreateSnapshot();
            Assert.AreEqual(1, snapshot.Agents.Count);
            Assert.AreEqual("TargetMissing", snapshot.Agents[0].SuppressedReason);
            Assert.AreEqual(0, snapshot.Agents[0].SelectedActionId);
            Assert.AreEqual("none", snapshot.Agents[0].LastCommand);
        }

        [Test]
        public void RuntimeAiPlannerPerception_VisibleTargetProjectsFacts()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 2f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            var source = new FakeRuntimeAiPlannerPerceptionSource();
            CharacterTestRuntimeAiPlannerEvaluator evaluator = CreateEvaluator(source);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision decision));

            Assert.IsTrue(decision.TargetExists);
            Assert.IsTrue(decision.TargetVisible);
            Assert.IsFalse(decision.TargetLineOfSightBlocked);
            Assert.AreEqual(0, decision.TargetLastSeenAgeFrames);
            Assert.Greater(decision.TargetThreatScore, 0f);
        }

        [Test]
        public void RuntimeAiPlannerPerception_BlockedTargetKeepsLastSeenAgeThenDecaysThreat()
        {
            using var harness = CreateHarness(
                CreatePerceptionMemoryProfile(memoryFrames: 1),
                disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 2f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            var source = new FakeRuntimeAiPlannerPerceptionSource();
            CharacterTestRuntimeAiPlannerEvaluator evaluator = CreateEvaluator(
                source,
                CreatePerceptionMemoryProfile(memoryFrames: 1));

            Assert.IsTrue(evaluator.TryEvaluate(ai, harness.CharacterEntities, new RuntimeFrame(1), out _));

            source.LineOfSightBlocked = true;
            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(2),
                out CharacterTestRuntimeAiPlannerDecision blockedDecision));
            Assert.IsTrue(blockedDecision.TargetLineOfSightBlocked);
            Assert.IsFalse(blockedDecision.TargetVisible);
            Assert.AreEqual(1, blockedDecision.TargetLastSeenAgeFrames);
            Assert.Greater(blockedDecision.TargetThreatScore, 0f);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(3),
                out CharacterTestRuntimeAiPlannerDecision expiredDecision));
            Assert.AreEqual(2, expiredDecision.TargetLastSeenAgeFrames);
            Assert.AreEqual(0f, expiredDecision.TargetThreatScore);
        }

        [Test]
        public void RuntimeAiPlannerPerception_SurfaceFactsProjectFromCharacterEntity()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 2f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            ai.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.zero,
                isGrounded: true,
                surfaceMaterial: CombatSurfaceMaterial.HardSlick);
            CharacterTestRuntimeAiPlannerEvaluator evaluator = CreateEvaluator(
                CharacterTestRuntimeAiPlannerWorldPerceptionSource.Empty);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision decision));

            Assert.AreEqual((int)CombatSurfaceMaterialKind.HardSlick, decision.SelfSurfaceKind);
            Assert.IsTrue(decision.SelfOnHardSlick);
        }

        [Test]
        public void RuntimeAiPlannerPerception_NearMovableObjectProjectsEnvironmentFacts()
        {
            CharacterTestMovableNaturalObjectSpawnSpec movable =
                CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                    "test.movable",
                    1,
                    CharacterTestMovableNaturalObjectSpawnSpec.DefaultCategory,
                    40f,
                    CombatSurfaceMaterial.Default,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(0.5f, 1f, 0f));
            using var harness = CreateHarness(
                disableLegacyStartup: true,
                movableSpecs: new[] { movable });
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 2f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));

            harness.Tick();

            CharacterTestRuntimeAiPlannerSnapshot snapshot =
                harness.Modules.RuntimeAiPlannerCommands.CreateSnapshot();
            Assert.AreEqual(1, snapshot.Agents.Count);
            Assert.IsTrue(snapshot.Agents[0].SelfNearMovableObject);
            Assert.IsTrue(snapshot.Agents[0].EnvironmentNearUsefulObject);
        }

        [Test]
        public void RuntimeAiPlannerUtilityTieBreak_ChoosesLowerActionIdWhenScoreAndCostMatch()
        {
            var profile = new CharacterTestRuntimeAiPlannerProfile(
                initialStateId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0017,
                states: new[]
                {
                    new CharacterTestRuntimeAiPlannerStateDefinition(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0017)
                },
                actions: new[]
                {
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        20,
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0017,
                        CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                        score: 10f,
                        cost: 1f,
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                                true)
                        }),
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        10,
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0017,
                        CharacterTestRuntimeAiPlannerActionKind.Retreat,
                        score: 10f,
                        cost: 1f,
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetExists,
                                true)
                        })
                });
            using var harness = CreateHarness(profile);
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 4f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));

            harness.Tick();

            RuntimeCommand command = FindRuntimeAiPlannerCommand(harness);
            Assert.AreEqual(CharacterTestRuntimeCommandSources.RuntimeAiPlanner, command.SourceId);
            CharacterTestRuntimeAiPlannerSnapshot snapshot =
                harness.Modules.RuntimeAiPlannerCommands.CreateSnapshot();
            Assert.AreEqual(10, snapshot.Agents[0].SelectedActionId);
        }

        [Test]
        public void RuntimeAiPlannerTargetDeadFact_TracksAliveDyingDeadAndNoTargetStates()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            CharacterTestCharacterEntity target = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 2f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            CharacterTestRuntimeAiPlannerEvaluator evaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource());

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision aliveDecision));
            Assert.IsTrue(aliveDecision.TargetExists);
            Assert.IsFalse(aliveDecision.TargetDead);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerTargetLifeState.Alive,
                aliveDecision.TargetLifeState);

            target.BeginDeath(frame: 2, sourceEntityId: ai.Id.Value, traceId: 10);
            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(2),
                out CharacterTestRuntimeAiPlannerDecision dyingDecision));
            Assert.IsTrue(dyingDecision.TargetExists);
            Assert.IsTrue(dyingDecision.TargetDead);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerTargetLifeState.Dying,
                dyingDecision.TargetLifeState);

            target.CompleteDeath(frame: 3);
            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(3),
                out CharacterTestRuntimeAiPlannerDecision deadDecision));
            Assert.IsTrue(deadDecision.TargetExists);
            Assert.IsTrue(deadDecision.TargetDead);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerTargetLifeState.Dead,
                deadDecision.TargetLifeState);

            target.BeginRevive(frame: 4, health: 30);
            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(4),
                out CharacterTestRuntimeAiPlannerDecision revivingDecision));
            Assert.IsTrue(revivingDecision.TargetExists);
            Assert.IsTrue(revivingDecision.TargetDead);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerTargetLifeState.Reviving,
                revivingDecision.TargetLifeState);

            using var noTargetHarness = CreateHarness(disableLegacyStartup: true);
            CharacterTestCharacterEntity isolatedAi = Spawn(
                noTargetHarness,
                new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 1f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled));
            CharacterTestRuntimeAiPlannerEvaluator noTargetEvaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource());

            Assert.IsTrue(noTargetEvaluator.TryEvaluate(
                isolatedAi,
                noTargetHarness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision noTargetDecision));
            Assert.IsFalse(noTargetDecision.TargetExists);
            Assert.IsFalse(noTargetDecision.TargetDead);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerTargetLifeState.None,
                noTargetDecision.TargetLifeState);
        }

        [Test]
        public void RuntimeAiPlannerDefaultMelee_WaitsWhileTargetIsInactiveAndResumesWhenAlive()
        {
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile());
            Assert.IsTrue(compile.Success, compile.Report.ToText());

            using var harness = CreateHarness(compile.Profile, disableLegacyStartup: true);
            CharacterTestCharacterEntity target = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 2f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            CharacterTestRuntimeAiPlannerEvaluator evaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource(), compile.Profile);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision aliveDecision));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                aliveDecision.SelectedActionKind);

            target.BeginDeath(frame: 2, sourceEntityId: ai.Id.Value, traceId: 13);
            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(2),
                out CharacterTestRuntimeAiPlannerDecision dyingDecision));
            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.Wait, dyingDecision.SelectedActionKind);

            target.CompleteDeath(frame: 3);
            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(3),
                out CharacterTestRuntimeAiPlannerDecision deadDecision));
            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.Wait, deadDecision.SelectedActionKind);

            target.BeginRevive(frame: 4, health: 30);
            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(4),
                out CharacterTestRuntimeAiPlannerDecision revivingDecision));
            Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.Wait, revivingDecision.SelectedActionKind);

            target.CompleteRevive(frame: 5);
            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(5),
                out CharacterTestRuntimeAiPlannerDecision resumedDecision));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                resumedDecision.SelectedActionKind);
        }

        [Test]
        public void RuntimeAiPlannerTargetDeadCondition_SelectsAliveAndDeadBranches()
        {
            var profile = new CharacterTestRuntimeAiPlannerProfile(
                initialStateId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0057,
                states: new[]
                {
                    new CharacterTestRuntimeAiPlannerStateDefinition(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0057)
                },
                actions: new[]
                {
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        10,
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0057,
                        CharacterTestRuntimeAiPlannerActionKind.Wait,
                        score: 10f,
                        cost: 0f,
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetDead,
                                false)
                        }),
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        20,
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0057,
                        CharacterTestRuntimeAiPlannerActionKind.Wait,
                        score: 10f,
                        cost: 0f,
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.Bool(
                                CharacterTestRuntimeAiPlannerFactKind.TargetDead,
                                true)
                        })
                });
            using var harness = CreateHarness(profile, disableLegacyStartup: true);
            CharacterTestCharacterEntity target = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 2f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            CharacterTestRuntimeAiPlannerEvaluator evaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource(), profile);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision aliveDecision));
            Assert.AreEqual(10, aliveDecision.SelectedActionId);

            target.BeginDeath(frame: 2, sourceEntityId: ai.Id.Value, traceId: 11);
            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(2),
                out CharacterTestRuntimeAiPlannerDecision deadDecision));
            Assert.AreEqual(20, deadDecision.SelectedActionId);
        }

        [Test]
        public void RuntimeAiPlannerTargetLifeStateCondition_SelectsExactLifecycleBranch()
        {
            var profile = new CharacterTestRuntimeAiPlannerProfile(
                initialStateId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0057,
                states: new[] { new CharacterTestRuntimeAiPlannerStateDefinition(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0057) },
                actions: new[]
                {
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        10,
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0057,
                        CharacterTestRuntimeAiPlannerActionKind.Wait,
                        score: 10f,
                        cost: 0f,
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.LifeState(
                                CharacterTestRuntimeAiPlannerTargetLifeState.Alive)
                        }),
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        20,
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0057,
                        CharacterTestRuntimeAiPlannerActionKind.Wait,
                        score: 10f,
                        cost: 0f,
                        conditions: new[]
                        {
                            CharacterTestRuntimeAiPlannerConditionDefinition.LifeState(
                                CharacterTestRuntimeAiPlannerTargetLifeState.Dying)
                        })
                });
            using var harness = CreateHarness(profile, disableLegacyStartup: true);
            CharacterTestCharacterEntity target = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 2f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer));
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            CharacterTestRuntimeAiPlannerEvaluator evaluator =
                CreateEvaluator(new FakeRuntimeAiPlannerPerceptionSource(), profile);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(1),
                out CharacterTestRuntimeAiPlannerDecision aliveDecision));
            Assert.AreEqual(10, aliveDecision.SelectedActionId);

            target.BeginDeath(frame: 2, sourceEntityId: ai.Id.Value, traceId: 12);
            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                harness.CharacterEntities,
                new RuntimeFrame(2),
                out CharacterTestRuntimeAiPlannerDecision dyingDecision));
            Assert.AreEqual(20, dyingDecision.SelectedActionId);
        }

        [Test]
        public void RuntimeAiPlannerTargetDeadFact_PreservesEnumValuesAndWorldStateContract()
        {
            Assert.AreEqual(20, (int)CharacterTestRuntimeAiPlannerFactKind.SelfDodgeCooldownReady);
            Assert.AreEqual(21, (int)CharacterTestRuntimeAiPlannerFactKind.TargetDead);
            Assert.AreEqual(22, (int)CharacterTestRuntimeAiPlannerFactKind.TargetLifeState);
            Assert.AreEqual(10, (int)CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady);
            Assert.AreEqual(13, (int)CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard);
            Assert.AreEqual(14, (int)CharacterTestRuntimeAiLabCardConditionFact.TargetDead);

            var facts = new CharacterTestRuntimeAiPlannerFactState();
            Assert.IsTrue(facts.Contains(CharacterTestRuntimeAiPlannerFactKeys.TargetDead));
            Assert.IsTrue(facts.Contains(CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState));
            Assert.AreEqual(false, facts.GetRawValue(CharacterTestRuntimeAiPlannerFactKeys.TargetDead));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerTargetLifeState.None,
                facts.GetRawValue(CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState));

            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.TargetDead, true);
            Assert.AreEqual(true, facts.GetRawValue(CharacterTestRuntimeAiPlannerFactKeys.TargetDead));
            Assert.AreEqual(
                true,
                facts.Snapshot()[CharacterTestRuntimeAiPlannerFactKeys.TargetDead]);

            Assert.IsTrue(facts.Remove(CharacterTestRuntimeAiPlannerFactKeys.TargetDead));
            Assert.AreEqual(false, facts.GetRawValue(CharacterTestRuntimeAiPlannerFactKeys.TargetDead));

            facts.SetValue(
                CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState,
                CharacterTestRuntimeAiPlannerTargetLifeState.Dying);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerTargetLifeState.Dying,
                facts.Snapshot()[CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState]);
            Assert.IsTrue(facts.Remove(CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerTargetLifeState.None,
                facts.GetRawValue(CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState));
        }

    }
}
