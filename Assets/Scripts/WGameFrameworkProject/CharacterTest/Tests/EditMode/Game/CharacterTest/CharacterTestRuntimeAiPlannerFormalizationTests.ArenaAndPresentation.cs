using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public void ArenaMatchConfig_DefaultBuildsAiSpawnSpecsWithBehaviorSelection()
        {
            CharacterTestArenaMatchConfig config = CharacterTestArenaMatchConfig.CreateDefault();
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtime =
                config.BehaviorGroups.CreateRuntime(
                    CharacterTestRuntimeAiPlannerProfileSource.CreateDefault(),
                    null,
                    NullRuntimeLogger.Instance);

            bool built = config.TryBuildSpawnSpecs(
                runtime,
                out CharacterTestCharacterSpawnSpec[] specs,
                out string message);

            Assert.IsTrue(built, message);
            Assert.AreEqual(2, specs.Length);
            Assert.AreEqual(CharacterTestArenaMatchConfig.Team1Id, specs[0].TeamId);
            Assert.AreEqual(CharacterTestArenaMatchConfig.Team2Id, specs[1].TeamId);
            Assert.AreEqual(CharacterTestCharacterControlOwner.AiControlled, specs[0].ControlOwner);
            Assert.AreEqual(CharacterTestCharacterControlOwner.AiControlled, specs[1].ControlOwner);
            Assert.IsTrue(specs[0].RuntimeAiBehaviorSelection.HasSelection);
            Assert.IsTrue(specs[1].RuntimeAiBehaviorSelection.HasSelection);
            Assert.AreEqual(
                specs[0].RuntimeAiBehaviorSelection.ProfileSlot,
                specs[0].RuntimeAiPlannerProfileSlot);
            Assert.AreEqual(
                runtime.CatalogHash,
                specs[0].RuntimeAiBehaviorSelection.CatalogHash);
            Assert.AreEqual(
                CharacterTestArenaBehaviorGroupDraftStore.DefaultDraftId,
                config.Team1Slots[0].BehaviorGroupDraftId);
            Assert.AreEqual(CharacterTestCameraMode.MultiTargetAuto, config.PreferredCameraMode);
            Assert.AreEqual(CharacterTestArenaMatchConfig.Team1Id, config.PresentationTeamId);
        }

        [Test]
        public void CharacterViewSync_UsesArenaPresentationTeamWhenNoLocalPlayerExists()
        {
            CharacterTestCharacterRuntimeWorld world = CreateViewSyncWorld(
                CharacterTestCharacterControlOwner.AiControlled,
                CharacterTestCharacterControlOwner.AiControlled);

            bool hasReferenceTeam = CharacterTestCharacterViewSyncSystem.TryResolvePresentationTeam(
                world.Entities,
                CharacterTestArenaMatchConfig.Team1Id,
                out int referenceTeamId);

            Assert.IsTrue(hasReferenceTeam);
            Assert.AreEqual(CharacterTestArenaMatchConfig.Team1Id, referenceTeamId);
            Assert.AreEqual(
                CharacterTestHeadPadAllegiance.Ally,
                CharacterTestRuntimeCharacterView.ResolveAllegiance(
                    CharacterTestArenaMatchConfig.Team1Id,
                    CharacterTestCharacterControlOwner.AiControlled,
                    hasReferenceTeam,
                    referenceTeamId));
            Assert.AreEqual(
                CharacterTestHeadPadAllegiance.Enemy,
                CharacterTestRuntimeCharacterView.ResolveAllegiance(
                    CharacterTestArenaMatchConfig.Team2Id,
                    CharacterTestCharacterControlOwner.AiControlled,
                    hasReferenceTeam,
                    referenceTeamId));
        }

        [Test]
        public void CharacterViewSync_KeepsNeutralWithoutLocalPlayerOrPresentationTeam()
        {
            CharacterTestCharacterRuntimeWorld world = CreateViewSyncWorld(
                CharacterTestCharacterControlOwner.AiControlled,
                CharacterTestCharacterControlOwner.AiControlled);

            bool hasReferenceTeam = CharacterTestCharacterViewSyncSystem.TryResolvePresentationTeam(
                world.Entities,
                presentationAllyTeamId: 0,
                out int referenceTeamId);

            Assert.IsFalse(hasReferenceTeam);
            Assert.AreEqual(0, referenceTeamId);
            Assert.AreEqual(
                CharacterTestHeadPadAllegiance.Neutral,
                CharacterTestRuntimeCharacterView.ResolveAllegiance(
                    CharacterTestArenaMatchConfig.Team1Id,
                    CharacterTestCharacterControlOwner.AiControlled,
                    hasReferenceTeam,
                    referenceTeamId));
        }

        [Test]
        public void CharacterViewSync_LocalPlayerTeamOverridesArenaPresentationTeam()
        {
            CharacterTestCharacterRuntimeWorld world = CreateViewSyncWorld(
                CharacterTestCharacterControlOwner.AiControlled,
                CharacterTestCharacterControlOwner.LocalPlayer);

            bool hasReferenceTeam = CharacterTestCharacterViewSyncSystem.TryResolvePresentationTeam(
                world.Entities,
                CharacterTestArenaMatchConfig.Team1Id,
                out int referenceTeamId);

            Assert.IsTrue(hasReferenceTeam);
            Assert.AreEqual(CharacterTestArenaMatchConfig.Team2Id, referenceTeamId);
            Assert.AreEqual(
                CharacterTestHeadPadAllegiance.Enemy,
                CharacterTestRuntimeCharacterView.ResolveAllegiance(
                    CharacterTestArenaMatchConfig.Team1Id,
                    CharacterTestCharacterControlOwner.AiControlled,
                    hasReferenceTeam,
                    referenceTeamId));
            Assert.AreEqual(
                CharacterTestHeadPadAllegiance.Player,
                CharacterTestRuntimeCharacterView.ResolveAllegiance(
                    CharacterTestArenaMatchConfig.Team2Id,
                    CharacterTestCharacterControlOwner.LocalPlayer,
                    hasReferenceTeam,
                    referenceTeamId));
        }

        [Test]
        public void CharacterViewSync_RuntimeAiLabKeepsOnlyAliveHeadPadsPersistent()
        {
            Assert.IsTrue(CharacterTestCharacterViewSyncSystem.ResolveHeadPadLockedVisible(
                lockedTargetEntityId: 0,
                entityId: 101,
                isAlive: true,
                forceHeadPadsVisible: true));
            Assert.IsFalse(CharacterTestCharacterViewSyncSystem.ResolveHeadPadLockedVisible(
                lockedTargetEntityId: 0,
                entityId: 101,
                isAlive: false,
                forceHeadPadsVisible: true));
            Assert.IsFalse(CharacterTestCharacterViewSyncSystem.ResolveHeadPadLockedVisible(
                lockedTargetEntityId: 202,
                entityId: 101,
                isAlive: false,
                forceHeadPadsVisible: false));
            Assert.IsFalse(CharacterTestCharacterViewSyncSystem.ResolveHeadPadLockedVisible(
                lockedTargetEntityId: 202,
                entityId: 202,
                isAlive: false,
                forceHeadPadsVisible: false));
            Assert.IsTrue(CharacterTestCharacterViewSyncSystem.ResolveHeadPadLockedVisible(
                lockedTargetEntityId: 202,
                entityId: 202,
                isAlive: true,
                forceHeadPadsVisible: false));
        }

        [Test]
        public void ArenaMatchConfig_CameraModeCycleUsesArenaOrder()
        {
            Assert.AreEqual(
                CharacterTestCameraMode.MultiTargetAuto,
                CharacterTestArenaSessionController.GetNextCameraMode(
                    CharacterTestCameraMode.ThirdPersonControlled));
            Assert.AreEqual(
                CharacterTestCameraMode.ThirdPersonControlled,
                CharacterTestArenaSessionController.GetNextCameraMode(
                    CharacterTestCameraMode.MultiTargetAuto));
        }

        [Test]
        public void RuntimeCommandRegistry_AcceptsOnlyExistingCameraModes()
        {
            var validator = new RuntimeCommandRegistryValidator(CharacterTestRuntimeCommandRegistry.Create());
            RuntimeCommand accepted = CharacterTestRuntimeCommandFactory.SetCameraMode(
                new RuntimeFrame(1),
                CharacterTestRuntimeCommandSources.CameraInput,
                CharacterTestCameraMode.MultiTargetAuto,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0040));
            var rejected = new RuntimeCommand(
                new RuntimeFrame(1),
                CharacterTestRuntimeCommandSources.CameraInput,
                CharacterTestRuntimeCommandIds.SetCameraMode,
                targetId: 0,
                payload0: 999);

            Assert.IsTrue(validator.Validate(accepted).Success);
            Assert.AreEqual(RuntimeCommandErrorCode.InvalidPayload, validator.Validate(rejected).Error.Code);
        }

        [Test]
        public void ArenaMatchConfig_RejectsMissingTeamAndUnavailableLoadout()
        {
            CharacterTestArenaMatchConfig missingTeam = CharacterTestArenaMatchConfig.CreateDefault();
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtime =
                missingTeam.BehaviorGroups.CreateRuntime(
                    CharacterTestRuntimeAiPlannerProfileSource.CreateDefault(),
                    null,
                    NullRuntimeLogger.Instance);
            missingTeam.Team2Slots[0].Enabled = false;

            Assert.IsFalse(missingTeam.TryBuildSpawnSpecs(
                runtime,
                out _,
                out string missingTeamMessage));
            StringAssert.Contains("Both arena teams", missingTeamMessage);

            CharacterTestArenaMatchConfig invalidLoadout = CharacterTestArenaMatchConfig.CreateDefault();
            invalidLoadout.Team1Slots[0].BehaviorGroupDraftId = 999;

            Assert.IsFalse(invalidLoadout.TryBuildSpawnSpecs(
                runtime,
                out _,
                out string invalidLoadoutMessage));
            StringAssert.Contains("loadout is not available", invalidLoadoutMessage);
        }

        [Test]
        public void ArenaBehaviorGroupDraftStore_EditsCompileIntoSessionRuntime()
        {
            CharacterTestArenaMatchConfig config = CharacterTestArenaMatchConfig.CreateDefault();
            CharacterTestArenaBehaviorGroupDraftStore store = config.BehaviorGroups;
            int draftId = config.Team1Slots[0].BehaviorGroupDraftId;

            Assert.IsTrue(store.TryGetDraft(draftId, out CharacterTestArenaBehaviorGroupDraft draft));
            int originalCount = draft.EntryCount;
            Assert.IsTrue(store.TryAddEntry(
                draftId,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                out string addMessage), addMessage);
            Assert.AreEqual(originalCount + 1, draft.EntryCount);
            int addedIndex = draft.EntryCount - 1;
            Assert.IsTrue(store.TryAdjustParameter(
                draftId,
                addedIndex,
                0,
                1,
                out string parameterMessage), parameterMessage);
            Assert.IsTrue(store.TryDuplicateEntry(
                draftId,
                addedIndex,
                out string duplicateMessage), duplicateMessage);
            Assert.IsTrue(store.TryMoveEntry(
                draftId,
                draft.EntryCount - 1,
                -1,
                out string moveMessage), moveMessage);
            Assert.IsTrue(store.TryToggleEntryDisabled(
                draftId,
                addedIndex,
                out string toggleMessage), toggleMessage);
            Assert.IsTrue(store.TryCompileAll(out string compileMessage), compileMessage);

            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtime =
                store.CreateRuntime(
                    CharacterTestRuntimeAiPlannerProfileSource.CreateDefault(),
                    null,
                    NullRuntimeLogger.Instance);
            Assert.IsTrue(runtime.Report.Success, runtime.Report.ToText());
            Assert.IsTrue(config.TryBuildSpawnSpecs(
                runtime,
                out CharacterTestCharacterSpawnSpec[] specs,
                out string spawnMessage), spawnMessage);
            Assert.AreEqual(
                CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(draftId),
                specs[0].RuntimeAiBehaviorSelection.LoadoutId);
        }

        [Test]
        public void RuntimeAiPlannerBehaviorActionChainPreview_WarnsOnEmptyAbilitySlot()
        {
            var hotbar = new CharacterTestAbilityHotbarRuntime(CharacterTestAbilityCatalogSource.CreateRegistry());
            Assert.IsTrue(hotbar.ClearSlot(1, out _));
            var action = new CharacterTestRuntimeAiPlannerActionDefinition(
                1,
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0018,
                CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot,
                score: 1f,
                cost: 1f,
                "Cast",
                abilitySlot: 1);

            CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem item =
                CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewBuilder.Build(
                    action,
                    hotbar,
                    CharacterTestCharacterActionConfigSource.Load());

            Assert.IsTrue(item.HasWarning);
            StringAssert.Contains("技能槽为空", item.WarningText);
        }

        [Test]
        public void RuntimeAiPlannerBehaviorActionChainPreview_UsesSelectedAbilitySlotFromSnapshot()
        {
            var snapshot = new CharacterTestRuntimeAiPlannerAgentSnapshot(
                entityId: 1,
                targetEntityId: 2,
                activeStatePath: "root/combat",
                targetExists: true,
                targetInAttackRange: true,
                targetDistance: 2f,
                movePathAvailable: true,
                selectedUtilityScore: 130f,
                selectedActionCost: 1.2f,
                selectedActionId: 930010,
                selectedActionName: "Cast ability slot",
                CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot,
                lastCommand: string.Empty,
                suppressedReason: string.Empty,
                rejectReason: string.Empty,
                transitionFromStateId: 0,
                transitionToStateId: 0,
                selectedAbilitySlot: 1);

            CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem item =
                CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewBuilder.BuildSelectedAction(
                    snapshot,
                    new CharacterTestAbilityHotbarRuntime(CharacterTestAbilityCatalogSource.CreateRegistry()),
                    CharacterTestCharacterActionConfigSource.Load());

            Assert.AreEqual(1, item.AbilitySlot);
            Assert.Greater(item.AbilityId, 0);
            Assert.IsTrue(item.HasCharacterAction, item.WarningText);
        }

        [Test]
        public void RuntimeAiPlannerProfileJson_LoadsV1WithDefaultPerceptionAndMovementWarning()
        {
            string json = @"{
  ""schema"": ""mx.character-test.runtime-ai-planner.profile.v1"",
  ""profileId"": ""legacy-v1"",
  ""initialStateId"": ""root"",
  ""targetSelectionMode"": ""NearestOpposingTeam"",
  ""attackRange"": 2.2,
  ""states"": [
    { ""stateId"": ""root"" }
  ],
  ""actions"": [
    {
      ""actionId"": 1,
      ""stateId"": ""root"",
      ""kind"": ""Wait"",
      ""score"": 0.0,
      ""cost"": 0.0
    }
  ]
}";

            bool loaded = CharacterTestRuntimeAiPlannerProfileJson.TryLoad(
                json,
                out CharacterTestRuntimeAiPlannerProfile profile,
                out CharacterTestRuntimeAiPlannerValidationReport report);

            Assert.IsTrue(loaded, report.ToText());
            AssertHasDiagnostic(report, "json.schema.legacy");
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultTargetMemoryFrames,
                profile.PerceptionSettings.TargetMemoryFrames);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultApproachLateralBlend,
                profile.MovementSettings.ApproachLateralBlend);
        }

        [Test]
        public void RuntimeAiPlannerProfileJson_LoadsV2WithDefaultMovementWarning()
        {
            string json = @"{
  ""schema"": ""mx.character-test.runtime-ai-planner.profile.v2"",
  ""profileId"": ""legacy-v2"",
  ""initialStateId"": ""root"",
  ""targetSelectionMode"": ""NearestOpposingTeam"",
  ""attackRange"": 2.2,
  ""perception"": {
    ""visionRange"": 12.0,
    ""targetMemoryFrames"": 30,
    ""lineOfSightObstacleLayerMask"": 0,
    ""environmentSampleRadius"": 2.5,
    ""threatDistanceWeight"": 1.0
  },
  ""states"": [
    { ""stateId"": ""root"" }
  ],
  ""actions"": [
    {
      ""actionId"": 1,
      ""stateId"": ""root"",
      ""kind"": ""Wait"",
      ""score"": 0.0,
      ""cost"": 0.0
    }
  ]
}";

            bool loaded = CharacterTestRuntimeAiPlannerProfileJson.TryLoad(
                json,
                out CharacterTestRuntimeAiPlannerProfile profile,
                out CharacterTestRuntimeAiPlannerValidationReport report);

            Assert.IsTrue(loaded, report.ToText());
            AssertHasDiagnostic(report, "json.schema.legacy");
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerMovementSettings.DefaultStrafeSwitchMinFrames,
                profile.MovementSettings.StrafeSwitchMinFrames);
        }

        [Test]
        public void RuntimeAiPlannerProfileValidator_ReportsInvalidPerceptionSettings()
        {
            var profile = new CharacterTestRuntimeAiPlannerProfile(
                initialStateId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060,
                states: new[]
                {
                    new CharacterTestRuntimeAiPlannerStateDefinition(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060)
                },
                actions: new[]
                {
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        1,
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060,
                        CharacterTestRuntimeAiPlannerActionKind.Wait,
                        0f,
                        0f)
                },
                perceptionSettings: new CharacterTestRuntimeAiPlannerPerceptionSettings(
                    float.NaN,
                    -1,
                    -1,
                    -0.1f,
                    float.PositiveInfinity));

            CharacterTestRuntimeAiPlannerValidationReport report =
                CharacterTestRuntimeAiPlannerProfileValidator.Validate(profile);

            AssertHasDiagnostic(report, "perception.visionRange");
            AssertHasDiagnostic(report, "perception.targetMemoryFrames");
            AssertHasDiagnostic(report, "perception.lineOfSightObstacleLayerMask");
            AssertHasDiagnostic(report, "perception.environmentSampleRadius");
            AssertHasDiagnostic(report, "perception.threatDistanceWeight");
        }

        [Test]
        public void RuntimeAiPlannerProfileValidator_ReportsInvalidMovementSettings()
        {
            var profile = new CharacterTestRuntimeAiPlannerProfile(
                initialStateId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060,
                states: new[]
                {
                    new CharacterTestRuntimeAiPlannerStateDefinition(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060)
                },
                actions: new[]
                {
                    new CharacterTestRuntimeAiPlannerActionDefinition(
                        1,
                        global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0060,
                        CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                        0f,
                        0f,
                        movementSettings: new CharacterTestRuntimeAiPlannerMovementSettings(
                            speedScale: 2f,
                            steeringResponsiveness: -0.1f,
                            approachLateralBlend: float.NaN,
                            preferredDistanceMin: 4f,
                            preferredDistanceMax: 2f,
                            arrivalSlowdownDistance: -1f,
                            strafeSwitchMinFrames: 10,
                            strafeSwitchMaxFrames: 5,
                            directionHoldFrames: -1,
                            reactionDelayFrames: -1))
                },
                movementSettings: new CharacterTestRuntimeAiPlannerMovementSettings(
                    speedScale: float.PositiveInfinity,
                    steeringResponsiveness: 2f,
                    approachLateralBlend: -0.1f,
                    preferredDistanceMin: -1f,
                    preferredDistanceMax: -2f,
                    arrivalSlowdownDistance: float.NegativeInfinity,
                    strafeSwitchMinFrames: -1,
                    strafeSwitchMaxFrames: -2,
                    directionHoldFrames: -1,
                    reactionDelayFrames: -1));

            CharacterTestRuntimeAiPlannerValidationReport report =
                CharacterTestRuntimeAiPlannerProfileValidator.Validate(profile);

            AssertHasDiagnostic(report, "movement.speedScale");
            AssertHasDiagnostic(report, "movement.steeringResponsiveness");
            AssertHasDiagnostic(report, "movement.approachLateralBlend");
            AssertHasDiagnostic(report, "movement.preferredDistanceMin");
            AssertHasDiagnostic(report, "movement.preferredDistanceMax");
            AssertHasDiagnostic(report, "movement.arrivalSlowdownDistance");
            AssertHasDiagnostic(report, "movement.strafeSwitchFrames");
            AssertHasDiagnostic(report, "movement.directionHoldFrames");
            AssertHasDiagnostic(report, "movement.reactionDelayFrames");
            AssertHasDiagnostic(report, "action.movement.speedScale");
            AssertHasDiagnostic(report, "action.movement.preferredDistanceRange");
        }

        [Test]
        public void RuntimeAiPlannerProfileJson_ReportsSchemaAndParseErrors()
        {
            bool parsed = CharacterTestRuntimeAiPlannerProfileJson.TryLoad(
                "{",
                out _,
                out CharacterTestRuntimeAiPlannerValidationReport parseReport);

            Assert.IsFalse(parsed);
            AssertHasDiagnostic(parseReport, "json.parse");

            parsed = CharacterTestRuntimeAiPlannerProfileJson.TryLoad(
                "{}",
                out _,
                out CharacterTestRuntimeAiPlannerValidationReport schemaReport);

            Assert.IsFalse(parsed);
            AssertHasDiagnostic(schemaReport, "json.schema.missing");
        }

        [Test]
        public void RuntimeAiPlannerProfileJson_DefaultFileLoads()
        {
            string fullPath = Path.GetFullPath(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    CharacterTestRuntimeAiPlannerProfileJson.DefaultProjectAssetPath));
            Assert.IsTrue(File.Exists(fullPath), fullPath);

            bool loaded = CharacterTestRuntimeAiPlannerProfileJson.TryLoad(
                File.ReadAllText(fullPath),
                out CharacterTestRuntimeAiPlannerProfile profile,
                out CharacterTestRuntimeAiPlannerValidationReport report);

            Assert.IsTrue(loaded, report.ToText());
            Assert.IsFalse(report.HasErrors, report.ToText());
            Assert.AreEqual(CharacterTestRuntimeAiPlannerProfileSource.ChaseStateId, profile.InitialStateId);
        }

        [Test]
        public void RuntimeAiPlannerProfileJson_MissingStreamingAssetsFileFallsBackToDefault()
        {
            CharacterTestRuntimeAiPlannerProfileLoadResult result =
                CharacterTestRuntimeAiPlannerProfileJson.LoadFromStreamingAssetsOrDefault(
                    "MxFramework/CharacterTest/runtime_ai_planner/missing-test-profile.json");

            Assert.IsTrue(result.UsedFallback);
            Assert.NotNull(result.Profile);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerProfileSource.DefaultProfileId,
                result.Profile.ProfileId);
            AssertHasDiagnostic(result.Report, "source.file.missing");
        }

    }
}
