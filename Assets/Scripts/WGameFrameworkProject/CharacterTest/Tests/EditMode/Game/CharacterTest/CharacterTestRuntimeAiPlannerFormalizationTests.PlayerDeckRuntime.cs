using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Physics;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Gameplay;
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
        public void CombatHud_MissingBehaviorCardMetadataUsesPlayerFacingPlaceholder()
        {
            Assert.IsFalse(
                CharacterTestFairyGuiCombatHudViewModelBuilder.TryResolveBehaviorCardMetadata(
                    null,
                    "card.internal-missing",
                    out string displayName,
                    out string iconKey));

            string playerFacingName =
                CharacterTestFairyGuiCombatHudViewModelBuilder.ResolveBehaviorCardDisplayName(
                    displayName,
                    null);

            Assert.AreEqual(string.Empty, iconKey);
            Assert.AreEqual("未知行为卡", playerFacingName);
            StringAssert.DoesNotContain("card.internal-missing", playerFacingName);
        }

        [Test]
        public void PlayerDeckSessionLock_AllowsReadOnlyCharacterObservationSwitch()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "mx-player-deck-locked-selection-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                using var harness = CreateHarness(
                    disableLegacyStartup: true,
                    runtimeAiPlannerProfileBindings: new[]
                    {
                        new CharacterTestRuntimeAiPlannerProfileBinding(
                            1,
                            CharacterTestRuntimeAiPlannerTemplateLibrary.SkillCasterProjectAssetPath,
                            CharacterTestRuntimeAiPlannerTemplateLibrary.CreateSkillCasterTemplate(),
                            null)
                    });
                Assert.IsTrue(CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    Path.Combine(directory, "deck.json"),
                    new[]
                    {
                        new CharacterTestRuntimeAiPlannerPlayerDeckTarget(1001, 0),
                        new CharacterTestRuntimeAiPlannerPlayerDeckTarget(2002, 1)
                    },
                    out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                    out string createError), createError);
                Assert.IsTrue(service.TryLockAndInstallForSession(
                    harness.Modules.RuntimeAiPlannerCommands,
                    Array.Empty<CharacterTestCharacterEntity>(),
                    out string lockError), lockError);

                Assert.AreEqual("character.definition.1001", service.SessionLock.CharacterStableId);
                Assert.IsTrue(service.Workbench.View.IsSessionLocked);
                Assert.IsTrue(service.CanSelectNextCharacter);
                Assert.IsTrue(service.TrySelectCharacter(1, out string selectError), selectError);
                Assert.AreEqual("character.definition.2002", service.SessionLock.CharacterStableId);
                Assert.IsTrue(service.Workbench.View.IsSessionLocked);
                Assert.IsFalse(service.Workbench.TryInstall(
                    "bc_3qz33zjzk4").Success);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void PlayerDeckSessionInstall_EmptyDeckUsesOnlyFreeFallback()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "mx-player-deck-fallback-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                using var harness = CreateHarness(disableLegacyStartup: true);
                Spawn(harness, new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                    teamId: 2,
                    x: 1f,
                    y: 1f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer));
                CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                    CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 1f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled));
                Assert.IsTrue(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                        Path.Combine(directory, "deck.json"),
                        CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                        out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                        out string createError),
                    createError);

                bool installed = service.TryLockAndInstallForSession(
                    harness.Modules.RuntimeAiPlannerCommands,
                    harness.CharacterEntities,
                    out string installError);
                harness.Tick();
                CharacterTestRuntimeAiPlannerSnapshot snapshot =
                    harness.Modules.RuntimeAiPlannerCommands.CreateSnapshot();
                bool presented = harness.Modules.RuntimeAiPlannerCommands.TryGetBehaviorCardRuntimePresentation(
                    ai.Id.Value,
                    ai.Control.RuntimeAiPlannerProfileSlot,
                    out _);
                CharacterTestFairyGuiCombatHudViewModel hud =
                    CharacterTestFairyGuiCombatHudViewModelBuilder.BuildInto(
                        new CharacterTestFairyGuiCombatHudViewModel(),
                        harness.Slice,
                        null,
                        null,
                        null,
                        null,
                        service.Workbench,
                        service);

                Assert.IsTrue(installed, installError);
                Assert.AreEqual(0, service.SessionLock.Cards.Count);
                Assert.AreEqual(CharacterTestRuntimeAiPlannerActionKind.Wait, snapshot.Agents[0].SelectedActionKind);
                Assert.IsFalse(presented);
                Assert.IsFalse(hud.BehaviorCardVisible);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void BehaviorCardOverlay_NonActionStageDoesNotPublishMainActionCandidate()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "wait"
            };
            payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = "wait",
                Title = "Wait",
                Kind = CharacterTestRuntimeAiLabCardStepKind.Wait,
                WaitFrames = 1
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.IsTrue(compile.Success);
            var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("test.wait-trace");
            AddBehaviorEntry(
                profile,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle);
            var overlay = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay(
                profile,
                new[] { compile.Program },
                harness.Modules.ActionRuntime,
                harness.Modules.AbilityRuntime,
                authorityHash: 1L);
            var facts = new CharacterTestRuntimeAiPlannerFactState();

            Assert.IsTrue(overlay.TryTick(ai, facts, new RuntimeFrame(0), out _));
            var store = new CharacterTestRuntimeAiLabDecisionTraceStore(1);
            CharacterTestRuntimeAiLabDecisionTraceWriter writer =
                store.BeginCapture(1L, 0, out var handle);
            overlay.WriteDecisionTraces(writer, 0);
            writer.Seal();
            Assert.IsTrue(store.TryGetAgent(handle, ai.Id.Value, out var trace));
            Assert.IsEmpty(trace.Commit.MainActionStableId);
            for (int i = 0; i < trace.Candidates.Count; i++)
                Assert.AreNotEqual(CharacterTestRuntimeAiLabDecisionTraceLayer.MainAction, trace.Candidates[i].Layer);
        }

        [Test]
        public void BehaviorCardOverlay_StableWaitTickDoesNotAllocateAfterWarmup()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "wait"
            };
            payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = "wait",
                Title = "Wait",
                Kind = CharacterTestRuntimeAiLabCardStepKind.Wait,
                WaitFrames = 10000
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.IsTrue(compile.Success);
            var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("test.wait-noalloc");
            AddBehaviorEntry(
                profile,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle);
            var overlay = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay(
                profile,
                new[] { compile.Program },
                harness.Modules.ActionRuntime,
                harness.Modules.AbilityRuntime,
                authorityHash: 2L);
            var facts = new CharacterTestRuntimeAiPlannerFactState();
            for (int frame = 0; frame < 8; frame++)
                Assert.IsTrue(overlay.TryTick(ai, facts, new RuntimeFrame(frame), out _));

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool ticked = true;
            for (int frame = 8; frame < 40; frame++)
                ticked &= overlay.TryTick(ai, facts, new RuntimeFrame(frame), out _);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(ticked);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void BehaviorTimeline_ActionCommandReplacesSameFrameAndExitTerminates()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1, x: 0f, y: 1f, z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            var actionId = CharacterTestCharacterActionConfigSource.LightAttackActionId;
            Assert.IsTrue(harness.Modules.ActionRuntime.TryStartActionSlot(ai,
                CharacterTestActionSlotIds.AttackPrimary, actionId, RuntimeFrame.Zero,
                new RuntimeTraceIdentity(1201311), CharacterActionSourceKind.RuntimeAiPlanner));
            Assert.IsTrue(harness.Modules.ActionRuntime.TryGetActiveActionPresentationState(ai.Id,
                out long oldInstance, out _, out _, out _));
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            { EntryTimelineId = "timeline.action" };
            var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
            { TimelineId = "timeline.action" };
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.action", TimelineId = timeline.TimelineId,
                TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                StartFrame = 0, DurationFrames = 10, MaxExecutionsPerPass = 1,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction, CharacterActionId = actionId }
            });
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.exit", TimelineId = timeline.TimelineId,
                TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                StartFrame = 1, DurationFrames = 9,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner }
            });
            payload.Timelines.Add(timeline);
            var compile = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.IsTrue(compile.Success);
            var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("test.timeline.same-frame");
            AddBehaviorEntry(profile, CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle);
            var overlay = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay(profile,
                new[] { compile.Program }, harness.Modules.ActionRuntime, harness.Modules.AbilityRuntime, authorityHash: 7L);
            var facts = new CharacterTestRuntimeAiPlannerFactState();
            bool started = false;
            bool exited = false;
            for (int f = 0; f < 8 && !exited; f++)
            {
                var frame = new RuntimeFrame(f);
                if (!overlay.TryTick(ai, facts, frame, out var intent)
                    || intent.Kind == CharacterTestRuntimeAiLabRuntimeIntentKind.None)
                    continue;
                Assert.AreEqual(CharacterTestRuntimeAiLabRuntimeIntentKind.BehaviorRecipeOrchestration, intent.Kind);
                Assert.IsTrue(harness.Modules.CharacterRuntime.TryApplyBehaviorRecipeOrchestration(frame,
                    ai.Id.Value, intent.OrchestrationOperation, intent.OrchestrationResourceId,
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0041), out string message), message);
                if (intent.OrchestrationOperation == CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction)
                {
                    Assert.IsTrue(harness.Modules.ActionRuntime.TryGetActiveActionPresentationState(ai.Id,
                        out long currentInstance, out CharacterActionId currentAction, out _, out _));
                    Assert.AreNotEqual(oldInstance, currentInstance);
                    Assert.AreEqual(actionId, currentAction);
                    started = true;
                }
                else if (intent.OrchestrationOperation == CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForReturnToPlanner)
                {
                    Assert.IsTrue(started);
                    Assert.IsFalse(harness.Modules.ActionRuntime.TryGetActiveActionPresentationState(ai.Id,
                        out _, out _, out _, out _));
                    exited = true;
                }
            }
            Assert.IsTrue(started && exited, "The bridge must both replace and terminate through the real action runtime.");
        }
        [Test]
        public void CharacterRuntime_V5AbilityOrchestrationCommitsCooldownOnlyAfterAtomicReplacement()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            Assert.IsTrue(harness.Modules.ActionRuntime.TryStartActionSlot(
                ai,
                CharacterTestActionSlotIds.AttackPrimary,
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                RuntimeFrame.Zero,
                new RuntimeTraceIdentity(1201312), CharacterActionSourceKind.RuntimeAiPlanner));
            Assert.IsTrue(harness.Modules.ActionRuntime.TryGetActiveActionPresentationState(
                ai.Id,
                out long openingInstanceId,
                out _,
                out _,
                out _));
            Assert.IsTrue(harness.Modules.AbilityRuntime.Definitions.TryGet(
                CharacterTestAbilityIds.DashStrikeAbilityId,
                out AbilityDefinitionConfig ability));

            RuntimeFrame frame = new RuntimeFrame(1);
            Assert.AreEqual(
                0L,
                harness.Modules.AbilityRuntime.GetRemainingCooldownFrames(ai.Id.Value, ability, frame));
            bool accepted = harness.Modules.CharacterRuntime.TryApplyBehaviorRecipeOrchestration(
                frame,
                ai.Id.Value,
                CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceAbility,
                ability.AbilityId,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0042),
                out string acceptedMessage);
            Assert.IsTrue(accepted, acceptedMessage);
            Assert.IsTrue(harness.Modules.ActionRuntime.TryGetActiveActionPresentationState(
                ai.Id,
                out long abilityInstanceId,
                out CharacterActionId abilityActionId,
                out _,
                out _));
            Assert.AreNotEqual(openingInstanceId, abilityInstanceId);
            Assert.AreEqual(ability.ActionBinding.ActionId, abilityActionId);
            long committedCooldown =
                harness.Modules.AbilityRuntime.GetRemainingCooldownFrames(ai.Id.Value, ability, frame);
            Assert.Greater(committedCooldown, 0L);

            bool rejected = harness.Modules.CharacterRuntime.TryApplyBehaviorRecipeOrchestration(
                frame,
                ai.Id.Value,
                CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceAbility,
                ability.AbilityId,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0043),
                out string rejectedMessage);
            Assert.IsFalse(rejected);
            StringAssert.Contains("OnCooldown", rejectedMessage);
            Assert.AreEqual(
                committedCooldown,
                harness.Modules.AbilityRuntime.GetRemainingCooldownFrames(ai.Id.Value, ability, frame));
            Assert.IsTrue(harness.Modules.ActionRuntime.TryGetActiveActionPresentationState(
                ai.Id,
                out long retainedInstanceId,
                out CharacterActionId retainedActionId,
                out _,
                out _));
            Assert.AreEqual(abilityInstanceId, retainedInstanceId);
            Assert.AreEqual(abilityActionId, retainedActionId);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult CompileSingleActionCard(
            string stageId,
            string actionId,
            CharacterTestRuntimeAiLabCardCondition activationCondition)
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = stageId
            };
            if (activationCondition != null)
                payload.ActivationConditions.Add(activationCondition);
            payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = stageId,
                Title = stageId,
                Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                CharacterActionId = CharacterActionLegacyFixtureIds.From(actionId)
            });
            return CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeTimeline CreateV5ActionTimeline(
            string timelineId,
            int order,
            string actionId)
        {
            return new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
            {
                TimelineId = timelineId,
                Order = order,
                Title = timelineId,
                EntryEvent = new CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent
                {
                    EventId = "entry." + timelineId
                },
                PrimaryActionEvent = new CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent
                {
                    EventId = "primary." + timelineId,
                    Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                    CharacterActionId = MxFramework.Tests.CharacterActionLegacyFixtureIds.From(actionId),
                    FallbackDurationFrames = 30
                }
            };
        }

        private static bool ApplyCharacterActionIntent(
            CharacterTestSliceTestHarness harness,
            CharacterTestCharacterEntity entity,
            CharacterTestRuntimeAiLabRuntimeIntent intent,
            RuntimeFrame frame)
        {
            if (intent.Kind != CharacterTestRuntimeAiLabRuntimeIntentKind.CharacterAction
                || !harness.Modules.ActionRuntime.Registry.TryGetAction(
                    new MxFramework.CharacterAction.CharacterActionId(intent.CharacterActionNumericId),
                    out CharacterActionConfig action))
            {
                return false;
            }

            int actionSlotId = action.Category == CharacterActionCategory.Dodge
                ? CharacterTestActionSlotIds.Dodge
                : CharacterTestActionSlotIds.AttackPrimary;
            return harness.Modules.ActionRuntime.TryStartActionSlot(
                entity,
                actionSlotId,
                action.ActionId,
                frame,
                new RuntimeTraceIdentity(1201313), CharacterActionSourceKind.RuntimeAiPlanner);
        }

        [Test]
        public void BehaviorCardOverlay_RestoredSameFrameDoesNotLeakDiscardedParameterizedObservation()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "wait"
            };
            var stage = new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = "wait",
                Title = "Wait",
                Kind = CharacterTestRuntimeAiLabCardStepKind.Wait,
                WaitFrames = 10
            };
            stage.EntryConditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "wait.ability",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true,
                AbilityId = CharacterTestAbilityIds.DashStrikeAbilityId
            });
            payload.Stages.Add(stage);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.IsTrue(compile.Success);
            var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("test.parameter-restore");
            AddBehaviorEntry(
                profile,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle);
            var overlay = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay(
                profile,
                new[] { compile.Program },
                harness.Modules.ActionRuntime,
                harness.Modules.AbilityRuntime,
                authorityHash: 3L);
            var facts = new CharacterTestRuntimeAiPlannerFactState();

            Assert.IsTrue(overlay.TryTick(ai, facts, new RuntimeFrame(0), out _));
            var firstStore = new CharacterTestRuntimeAiLabDecisionTraceStore(1);
            CharacterTestRuntimeAiLabDecisionTraceWriter firstWriter =
                firstStore.BeginCapture(3L, 0, out var firstHandle);
            overlay.WriteDecisionTraces(firstWriter, 0);
            firstWriter.Seal();
            Assert.IsTrue(firstStore.TryGetAgent(firstHandle, ai.Id.Value, out var firstTrace));
            Assert.IsTrue(ContainsParameterizedFact(firstTrace));
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot snapshot = overlay.CreateStateSnapshot(0);
            Assert.IsTrue(overlay.TryApplyStateSnapshot(snapshot, out string restoreError), restoreError);

            Assert.IsTrue(overlay.TryTick(ai, facts, new RuntimeFrame(0), out _));
            var replayStore = new CharacterTestRuntimeAiLabDecisionTraceStore(1);
            CharacterTestRuntimeAiLabDecisionTraceWriter replayWriter =
                replayStore.BeginCapture(3L, 0, out var replayHandle);
            overlay.WriteDecisionTraces(replayWriter, 0);
            replayWriter.Seal();
            Assert.IsTrue(replayStore.TryGetAgent(replayHandle, ai.Id.Value, out var replayTrace));
            Assert.IsFalse(ContainsParameterizedFact(replayTrace));
        }

        [Test]
        public void BehaviorCardOverlay_TerminalReleasePreservesRejectedCommandResult()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "move"
            };
            payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = "move",
                Title = "Move",
                Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget,
                MoveMinDistance = 0f,
                MoveMaxDistance = 10f
            });
            var detector = new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = "finish",
                Title = "Finish",
                Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                StageId = "move",
                Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                Priority = 10
            };
            detector.Conditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "finish.visible",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetVisible,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true
            });
            payload.Detectors.Add(detector);
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "finish.end",
                DetectorId = "finish",
                SourceStageId = "move",
                TargetStageId = CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                Priority = 10,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.IsTrue(compile.Success);
            var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("test.terminal-release");
            AddBehaviorEntry(
                profile,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack);
            var overlay = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay(
                profile,
                new[] { compile.Program },
                harness.Modules.ActionRuntime,
                harness.Modules.AbilityRuntime,
                authorityHash: 2L);
            var facts = new CharacterTestRuntimeAiPlannerFactState();
            facts.SetTarget(
                targetEntityId: 1,
                targetDistance: 3f,
                new CharacterTestAiNavigationResult(Vector2.right, true, false, 0),
                selfActionRunning: false,
                targetVisible: true,
                targetLineOfSightBlocked: false,
                targetLastSeenAgeFrames: 0,
                targetThreatScore: 1f);

            Assert.IsTrue(overlay.TryTick(ai, facts, new RuntimeFrame(0), out _));
            overlay.RecordCommandResult(ai.Id.Value, RuntimeCommandValidationResult.Accepted(default));
            Assert.IsTrue(overlay.TryTick(ai, facts, new RuntimeFrame(1), out _));
            overlay.RecordCommandResult(ai.Id.Value, RuntimeCommandValidationResult.Accepted(default));
            Assert.IsTrue(overlay.TryTick(ai, facts, new RuntimeFrame(2), out var terminalIntent));
            Assert.AreEqual(CharacterTestRuntimeAiLabRuntimeIntentKind.Control, terminalIntent.Kind);
            Assert.AreEqual(-1, overlay.CreateStateSnapshot(0).Agents[0].ActiveBindingIndex);
            overlay.RecordCommandResult(
                ai.Id.Value,
                RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidPayload,
                    default,
                    new RuntimeFrame(2),
                    "terminal release rejected")));
            var store = new CharacterTestRuntimeAiLabDecisionTraceStore(1);
            CharacterTestRuntimeAiLabDecisionTraceWriter writer =
                store.BeginCapture(2L, 2, out var handle);
            overlay.WriteDecisionTraces(writer, 2);
            writer.Seal();
            Assert.IsTrue(store.TryGetAgent(handle, ai.Id.Value, out var trace));
            Assert.AreEqual(CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected, trace.Commit.CommandEnqueueKind);
            Assert.AreEqual(CharacterTestRuntimeAiLabPlannerLogicCommitKind.Committed, trace.Commit.PlannerLogicKind);
            Assert.AreEqual(CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved, trace.Commit.RuntimeEffectKind);
        }

        [Test]
        public void PlayerDeckRuntime_SwitchReasonRejectsUnsupportedConditionShapes()
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "wait"
            };
            payload.ActivationConditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "ability.unavailable",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = false,
                AbilityId = CharacterTestAbilityIds.DashStrikeAbilityId
            });
            payload.ActivationConditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "arbitrary.range",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Between,
                Value = 0f,
                MaxValue = 2f
            });
            payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = "wait",
                Title = "Wait",
                Kind = CharacterTestRuntimeAiLabCardStepKind.Wait,
                WaitFrames = 1
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);

            Assert.IsTrue(compile.Success);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason.ConditionsSatisfied,
                CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ClassifySwitchReason(compile.Program));
        }

        [Test]
        public void RuntimeAiPlannerSpawnCommand_RoundTripsProfileSlot()
        {
            var spec = new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 7,
                x: 1f,
                y: 2f,
                z: 3f,
                CharacterTestCharacterControlOwner.AiControlled,
                runtimeAiPlannerProfileSlot: 5);

            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SpawnCharacter(
                RuntimeFrame.Zero,
                CharacterTestRuntimeCommandSources.System,
                spec);
            CharacterTestCharacterSpawnRequest request =
                CharacterTestRuntimeCommandFactory.DecodeSpawnCharacter(command);

            Assert.AreEqual(7, request.TeamId);
            Assert.AreEqual(CharacterTestCharacterControlOwner.AiControlled, request.ControlOwner);
            Assert.AreEqual(5, request.RuntimeAiPlannerProfileSlot);
        }

        [Test]
        public void RuntimeAiPlannerSpawnCommand_RoundTripsBehaviorSelection()
        {
            const long catalogHash = 0x1234567890L;
            var selection = new CharacterTestRuntimeAiBehaviorSelection(
                loadoutId: 10001,
                variantId: 3,
                catalogHash,
                profileSlot: 5);
            var spec = new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 1f,
                y: 2f,
                z: 3f,
                CharacterTestCharacterControlOwner.AiControlled,
                runtimeAiBehaviorSelection: selection);

            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SpawnCharacter(
                RuntimeFrame.Zero,
                CharacterTestRuntimeCommandSources.System,
                spec);
            CharacterTestCharacterSpawnRequest request =
                CharacterTestRuntimeCommandFactory.DecodeSpawnCharacter(command);

            Assert.IsTrue(command.Envelope.IsPresent);
            Assert.AreEqual(
                RuntimeCommandProvenanceKind.Derived,
                command.Envelope.Provenance);
            Assert.IsTrue(request.RuntimeAiBehaviorSelection.HasSelection);
            Assert.AreEqual(10001, request.RuntimeAiBehaviorSelection.LoadoutId);
            Assert.AreEqual(3, request.RuntimeAiBehaviorSelection.VariantId);
            Assert.AreEqual(catalogHash, request.RuntimeAiBehaviorSelection.CatalogHash);
            Assert.AreEqual(5, request.RuntimeAiBehaviorSelection.ProfileSlot);
            Assert.AreEqual(5, request.RuntimeAiPlannerProfileSlot);
        }

        [Test]
        public void RuntimeAiPlannerBehaviorSelectionCommand_RequiresUnlockedPhase()
        {
            using var harness = CreateHarness(disableLegacyStartup: true);
            CharacterTestCharacterEntity ai = Spawn(harness, new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 2,
                x: 0f,
                y: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled));

            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetRuntimeAiBehaviorSelection(
                harness.CurrentFrame,
                CharacterTestRuntimeCommandSources.System,
                ai.Id.Value,
                loadoutId: 1,
                variantId: 1);
            RuntimeCommandValidationResult enqueue = harness.Enqueue(command);
            Assert.IsTrue(enqueue.Success, enqueue.Error.Message);

            harness.Tick();

            Assert.IsFalse(ai.Control.RuntimeAiBehaviorSelection.HasSelection);
            Assert.That(
                harness.Modules.RuntimeCommands.RecentEvents,
                Has.Some.Matches<CharacterTestRuntimeEvent>(
                    evt => evt.Type == CharacterTestRuntimeEventType.RuntimeAiBehaviorSelectionRejected));
        }

    }
}
