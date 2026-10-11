using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevTests
    {

        [Test]
        public void CurrentBehaviorSummary_SeparatesCommittedCardFromViewedAndRejectedPendingCard()
        {
            CharacterTestRuntimeAiLabRevisionCardIdentity committed =
                CreateGameplaySummaryIdentity(0, "轻攻击");
            CharacterTestRuntimeAiLabRevisionCardIdentity pending =
                CreateGameplaySummaryIdentity(1, "闪避");
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
            {
                BehaviorCardIdentity = new CharacterTestRuntimeAiLabBehaviorCardIdentityProjection
                {
                    EditCard = new CharacterTestRuntimeAiLabEditCardIdentity
                    {
                        DraftEntryId = "viewed-dodge",
                        DisplayName = "闪避"
                    },
                    Runtime = new CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution
                    {
                        CommittedCard = committed,
                        CurrentStageId = "action.light-attack",
                        CurrentStageTitle = "轻攻击",
                        PrimaryAction = new CharacterTestRuntimeAiLabPrimaryActionIdentity
                        {
                            Available = true,
                            DisplayName = "轻攻击"
                        }
                    }
                }
            };
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            timeline.Available = true;
            timeline.StageCount = 1;
            timeline.Stages = new[]
            {
                new CharacterTestRuntimeAiLabLogicStageViewModel
                {
                    StageId = "viewed.action.evade",
                    Title = "闪避",
                    Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                    CharacterActionId = CharacterTestCharacterActionConfigSource.DefaultEvadeActionId,
                    Current = false
                }
            };

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FillCurrentBehaviorSummary(viewModel, default, default, hasSelectedAgent: true);

            Assert.AreEqual("当前：轻攻击｜查看：闪避", timeline.CurrentBehaviorTitleText);
            Assert.AreEqual("轻攻击", timeline.CurrentBehaviorCommandText);
            Assert.AreEqual("查看卡不是当前执行卡", timeline.CurrentBehaviorFactText);
            Assert.IsFalse(timeline.CurrentBehaviorWarningVisible);
            Assert.IsFalse(timeline.Stages[0].Current, "A viewed card must not inherit runtime highlighting.");
            Assert.IsTrue(timeline.BehaviorRelationshipAvailable);
            Assert.AreEqual("当前行为\n轻攻击", timeline.CommittedBehaviorText);
            Assert.AreEqual(0, timeline.CommittedBehaviorEntryIndex);
            Assert.AreEqual("下一 · 待决策", timeline.NextBehaviorText);
            Assert.IsFalse(timeline.NextBehaviorPending);

            viewModel.BehaviorCardIdentity.Runtime.CommittedCard = null;
            viewModel.BehaviorCardIdentity.Runtime.PendingCard = pending;
            viewModel.BehaviorCardIdentity.Runtime.PrimaryAction.Available = false;
            viewModel.BehaviorCardIdentity.Runtime.PrimaryAction.CommandRejected = true;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FillCurrentBehaviorSummary(viewModel, default, default, hasSelectedAgent: true);

            Assert.AreEqual("候选闪避 · 未执行", timeline.CurrentBehaviorTitleText);
            Assert.AreEqual("候选闪避 · 未执行", timeline.CurrentBehaviorFactText);
            Assert.IsTrue(timeline.CurrentBehaviorWarningVisible);
            StringAssert.Contains("动作未执行", timeline.CurrentBehaviorWarningText);
            Assert.AreEqual("当前行为\n尚无行为", timeline.CommittedBehaviorText);
            Assert.AreEqual("下一 · 闪避", timeline.NextBehaviorText);
            Assert.AreEqual("→ 候选", timeline.NextBehaviorRelationText);
            Assert.AreEqual(1, timeline.NextBehaviorEntryIndex);
            Assert.IsTrue(timeline.NextBehaviorPending);
        }

        private static CharacterTestRuntimeAiLabRevisionCardIdentity CreateGameplaySummaryIdentity(
            int bindingIndex,
            string displayName)
        {
            var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                null,
                null,
                displayName,
                "Test",
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle));
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(card.LogicPayload);
            Assert.IsTrue(compile.Success, "Identity fixture behavior card must compile.");
            var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                enabled: true,
                order: bindingIndex);
            var entry = new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                null,
                card.CardDraftId,
                runtimeEntry.BehaviorId,
                bindingIndex,
                disabled: false,
                runtimeEntry);
            Assert.IsTrue(
                CharacterTestRuntimeAiLabRevisionCardIdentity.TryCreate(
                    bindingIndex,
                    entry,
                    card,
                    compile.Program,
                    out CharacterTestRuntimeAiLabRevisionCardIdentity identity,
                    out string diagnostic),
                diagnostic);
            return identity;
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_ReactiveDodgeTemplateCompilesAppliesAndProjectsGameplayUi()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            Assert.IsTrue(controller.TryCreateReactiveChaseDodgeGroup(0, out string createMessage), createMessage);
            Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft reactiveGroup));
            int dodgeEntryIndex = -1;
            for (int entryIndex = 0; entryIndex < reactiveGroup.Entries.Count; entryIndex++)
            {
                if (string.Equals(
                        reactiveGroup.Entries[entryIndex].BehaviorId,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                        StringComparison.Ordinal))
                {
                    dodgeEntryIndex = entryIndex;
                    break;
                }
            }
            Assert.GreaterOrEqual(dodgeEntryIndex, 0);
            Assert.AreEqual(CharacterTestRuntimeAiLabCandidateState.PreviewComplete, controller.CandidateState);
            Assert.IsTrue(controller.DraftWorkspace.TryReplaceCardPayload(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                dodgeEntryIndex,
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    CharacterTestRuntimeAiPlannerActionKind.MoveToTarget),
                out string mismatchMessage), mismatchMessage);
            Assert.IsTrue(controller.TrySetGameplayDodgeCooldown(
                dodgeEntryIndex,
                31,
                out string mismatchBuildMessage), mismatchBuildMessage);
            Assert.AreEqual(CharacterTestRuntimeAiLabCandidateState.PreviewComplete, controller.CandidateState);
            Assert.IsTrue(controller.TryResetGameplayCard(dodgeEntryIndex, out string resetMessage), resetMessage);
            Assert.AreEqual(CharacterTestRuntimeAiLabCandidateState.PreviewComplete, controller.CandidateState);
            Assert.IsNotNull(controller.CandidateRevision);
            long candidateHash = controller.CandidateRevision.AuthorityHash;

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel behaviorCard =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                    controller,
                    commandStatus: null,
                    workspaceMode: CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                    selectedWorkbenchEntryIndex: dodgeEntryIndex);
            Assert.AreEqual(
                0,
                behaviorCard.WorkbenchRows.Length,
                "Inactive Behavior Group projections must not be built inside the Behavior Card workspace.");
            Assert.IsTrue(behaviorCard.Workspace.ShowBehaviorCard);
            Assert.IsTrue(behaviorCard.BehaviorCardWorkspace.Timeline.Available);
            Assert.AreEqual("运行调试", behaviorCard.ToggleDetailsLabel);
            Assert.Greater(behaviorCard.BehaviorCardWorkspace.Timeline.StageCount, 0);
            Assert.Greater(behaviorCard.BehaviorCardWorkspace.Timeline.DetectorCount, 0);

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            Assert.IsNotNull(panelHost);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink = panelHost.CommandSink;
            Assert.IsNotNull(sink);
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start,
                null));
            panelHost.Refresh();
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerDevScenePhase.Running,
                controller.Phase,
                sink.LastStatusMessage);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics,
                panelHost.CommandSink.WorkspaceMode);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame,
                panelHost.CommandSink.DiagnosticsTab);
            Assert.AreEqual(CharacterTestRuntimeAiLabCandidateState.Applied, controller.CandidateState);
            Assert.IsNotNull(controller.ActiveRevision);
            Assert.AreEqual(candidateHash, controller.ActiveRevision.AuthorityHash);
            CharacterTestRuntimeAiPlannerSnapshot startedPlanner =
                controller.CaptureRuntimeAiPlannerSnapshotForLab();
            Assert.GreaterOrEqual(controller.SelectedAgentIndex, 0);
            Assert.Less(controller.SelectedAgentIndex, startedPlanner.Agents.Count);
            CharacterTestArenaSlotConfig selectedSetupSlot = controller.MatchConfig.TryGetSlot(
                controller.SelectedSetupTeamId,
                controller.SelectedSetupSlotIndex);
            Assert.IsNotNull(selectedSetupSlot);
            Assert.AreEqual(
                CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                    selectedSetupSlot.BehaviorGroupDraftId),
                startedPlanner.Agents[controller.SelectedAgentIndex].RuntimeAiBehaviorLoadoutId,
                "The default observed Agent must own the selected behavior-card loadout.");
            int baselineFrame = controller.Timeline.LatestFrame;
            Assert.IsTrue(controller.TrySetGameplayDodgeCooldown(
                dodgeEntryIndex,
                45,
                out string cooldownMessage), cooldownMessage);
            Assert.AreNotEqual(candidateHash, controller.CandidateRevision.AuthorityHash);
            Assert.IsTrue(
                controller.TryResolveStageEditIdentity(
                    dodgeEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity dodgeCanonicalizationIdentity,
                    out string dodgeCanonicalizationIdentityMessage),
                dodgeCanonicalizationIdentityMessage);
            Assert.IsTrue(
                controller.TryCanonicalizeLogicDetectorWindows(
                    dodgeEntryIndex,
                    dodgeCanonicalizationIdentity,
                    out _,
                    out string dodgeCanonicalizationMessage),
                dodgeCanonicalizationMessage);
            long revisedHash = controller.CandidateRevision.AuthorityHash;
            Assert.IsTrue(controller.QueueCandidateApply(), controller.StatusMessage);
            Assert.AreEqual(revisedHash, controller.QueuedCandidateRevision.AuthorityHash);
            int pendingDraftRevision = controller.DraftWorkspace.CurrentRevision;
            Assert.IsFalse(
                controller.TrySetGameplayDodgeCooldown(dodgeEntryIndex, 46, out _),
                "Apply Pending must reject semantic mutations before F+1.");
            Assert.AreEqual(pendingDraftRevision, controller.DraftWorkspace.CurrentRevision);
            Assert.AreEqual(revisedHash, controller.CandidateRevision.AuthorityHash);
            Assert.IsTrue(controller.TryResolveStageEditIdentity(
                dodgeEntryIndex,
                out CharacterTestRuntimeAiLabStageEditIdentity pendingIdentity,
                out string pendingIdentityDiagnostic), pendingIdentityDiagnostic);
            Assert.IsFalse(controller.TryUndoBehaviorCardEdit(
                dodgeEntryIndex,
                pendingIdentity,
                out _,
                out _), "Apply Pending must reject history navigation before F+1.");
            yield return null;
            yield return null;
            Assert.AreEqual(CharacterTestRuntimeAiLabCandidateState.Applied, controller.CandidateState);
            Assert.AreEqual(revisedHash, controller.ActiveRevision.AuthorityHash);
            Assert.AreEqual(1, controller.AppliedRevisionEventCount);
            Assert.AreEqual(baselineFrame + 1, controller.LastAppliedRevisionFrame);
            Assert.IsTrue(controller.StepFrame(), controller.StatusMessage);
            int revisedFrame = controller.Timeline.LatestFrame;
            Assert.AreEqual(baselineFrame + 1, revisedFrame);
            Assert.IsTrue(controller.JumpToFrame(baselineFrame), controller.StatusMessage);
            Assert.IsTrue(controller.JumpToFrame(revisedFrame), controller.StatusMessage);

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel running =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                    controller,
                    commandStatus: null,
                    workspaceMode: CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics,
                    selectedWorkbenchEntryIndex: dodgeEntryIndex,
                    diagnosticsTab: CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame);
            Assert.IsNotNull(running.TimelineProjection);
            bool hasDecisionItem = false;
            for (int trackIndex = 0; trackIndex < running.TimelineProjection.Tracks.Count; trackIndex++)
            {
                CharacterTestRuntimeAiLabTimelineTrackProjection track =
                    running.TimelineProjection.Tracks[trackIndex];
                if (track.Kind == CharacterTestRuntimeAiLabTimelineTrackKind.Decision
                    && track.Items.Count > 0)
                {
                    hasDecisionItem = true;
                }
            }
            Assert.IsTrue(hasDecisionItem, "Applied candidate decisions must be reviewable on the formal timeline.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_MultiStageV2AppliesAndRecordsTypedStageMarker()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            Assert.IsTrue(controller.TryCreateReactiveChaseDodgeGroup(0, out string createMessage), createMessage);
            Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft multiStageGroup));
            int dodgeEntryIndex = -1;
            int logicEntryIndex = -1;
            for (int entryIndex = 0; entryIndex < multiStageGroup.Entries.Count; entryIndex++)
            {
                string behaviorId = multiStageGroup.Entries[entryIndex].BehaviorId;
                if (string.Equals(
                        behaviorId,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                        StringComparison.Ordinal))
                    dodgeEntryIndex = entryIndex;
                else if (string.Equals(
                        behaviorId,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                        StringComparison.Ordinal))
                    logicEntryIndex = entryIndex;
            }
            Assert.GreaterOrEqual(dodgeEntryIndex, 0);
            Assert.GreaterOrEqual(logicEntryIndex, 0);
            Assert.IsTrue(
                controller.DraftWorkspace.TryToggleEntry(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    dodgeEntryIndex,
                    out string disableDodgeMessage),
                disableDodgeMessage);
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload pursuit =
                CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(
                    CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture());
            Assert.IsTrue(
                CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryCanonicalizeDetectorWindows(
                    pursuit,
                    out _,
                    out string pursuitCanonicalizationMessage),
                pursuitCanonicalizationMessage);
            Assert.Greater(pursuit.Stages.Count, 1);
            Assert.IsTrue(controller.SetSetupSpawnPosition(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                new Vector2(-2.5f, 0f)));
            Assert.IsTrue(controller.SetSetupSpawnPosition(
                CharacterTestArenaMatchConfig.Team2Id,
                0,
                new Vector2(2.5f, 0f)));
            Assert.IsTrue(controller.SelectSetupSpawnSlot(CharacterTestArenaMatchConfig.Team1Id, 0));
            Assert.IsTrue(
                controller.TryResolveStageEditIdentity(
                    logicEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity existingCanonicalizationIdentity,
                    out string existingCanonicalizationIdentityMessage),
                existingCanonicalizationIdentityMessage);
            Assert.IsTrue(
                controller.TryCanonicalizeLogicDetectorWindows(
                    logicEntryIndex,
                    existingCanonicalizationIdentity,
                    out _,
                    out string existingCanonicalizationMessage),
                existingCanonicalizationMessage);
            Assert.IsTrue(
                controller.TryReplaceSelectedCardLogic(
                    logicEntryIndex,
                    pursuit,
                    out string replaceMessage),
                replaceMessage);
            Assert.IsTrue(
                controller.TryResolveStageEditIdentity(
                    logicEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity canonicalizationIdentity,
                    out string canonicalizationIdentityMessage),
                canonicalizationIdentityMessage);
            Assert.IsTrue(
                controller.TryCanonicalizeLogicDetectorWindows(
                    logicEntryIndex,
                    canonicalizationIdentity,
                    out _,
                    out string canonicalizationMessage),
                canonicalizationMessage);
            Assert.AreEqual(CharacterTestRuntimeAiLabCandidateState.PreviewComplete, controller.CandidateState);
            Assert.IsNotNull(controller.CandidateRevision);
            Assert.Greater(controller.CandidateRevision.LogicPrograms[logicEntryIndex].Stages.Count, 1);
            long semanticBeforeTiming =
                controller.CandidateRevision.LogicPrograms[logicEntryIndex].SemanticHash;
            int lastStageIndex = pursuit.Stages.Count - 1;
            Assert.IsTrue(
                controller.TryMoveLogicStageColumn(
                    logicEntryIndex,
                    lastStageIndex,
                    1,
                    out string timingMessage),
                timingMessage);
            Assert.IsNotNull(controller.CandidateRevision, controller.CandidateDiagnostic);
            Assert.AreNotEqual(
                semanticBeforeTiming,
                controller.CandidateRevision.LogicPrograms[logicEntryIndex].SemanticHash,
                "Moving a stage horizontally edits its planned start time and must change behavior semantics.");
            long authorityBeforePriority = controller.CandidateRevision.AuthorityHash;
            Assert.IsTrue(
                controller.TryAdjustLogicDetectorPriority(
                    logicEntryIndex,
                    0,
                    -1,
                    out string priorityMessage),
                priorityMessage);
            Assert.AreNotEqual(authorityBeforePriority, controller.CandidateRevision.AuthorityHash);

            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            yield return null;
            yield return null;
            Assert.AreEqual(CharacterTestRuntimeAiLabCandidateState.Applied, controller.CandidateState);
            Assert.IsTrue(controller.StepFrames(80), controller.StatusMessage);

            int indexedStageMarkerFrame = -1;
            int indexedStageMarkerEntityId = 0;
            for (int frame = controller.Timeline.RetainedStartFrame;
                 frame <= controller.Timeline.LatestFrame && indexedStageMarkerFrame < 0;
                 frame++)
            {
                CharacterTestRuntimeAiLabLogicEventBuffer frameEvents =
                    controller.GetBehaviorLogicEventsForFrame(frame);
                for (int eventIndex = 0; eventIndex < frameEvents.Count; eventIndex++)
                {
                    if (frameEvents[eventIndex].Kind
                        == CharacterTestRuntimeAiLabLogicEventKind.StageEntered)
                    {
                        indexedStageMarkerFrame = frame;
                        indexedStageMarkerEntityId = frameEvents[eventIndex].EntityId;
                        break;
                    }
                }
            }
            CharacterTestRuntimeAiPlannerSnapshot planner =
                controller.CaptureRuntimeAiPlannerSnapshotForLab();
            int markerAgentIndex = -1;
            for (int agentIndex = 0;
                 planner?.Agents != null && agentIndex < planner.Agents.Count;
                 agentIndex++)
            {
                if (planner.Agents[agentIndex].EntityId == indexedStageMarkerEntityId)
                {
                    markerAgentIndex = agentIndex;
                    break;
                }
            }
            Assert.GreaterOrEqual(markerAgentIndex, 0);
            Assert.IsTrue(controller.SelectAgent(markerAgentIndex));
            CharacterTestRuntimeAiLabTimelineProjection projection = controller.BuildTimelineProjection();
            bool foundStageMarker = false;
            var observedItems = new List<string>();
            CharacterTestRuntimeAiPlannerStateSnapshot plannerState = controller.CaptureRuntimeAiPlannerStateForLab();
            string plannerDiagnostic = " agents=" + (planner?.Agents?.Count ?? -1)
                + " logicSlots=" + (plannerState?.BehaviorLogicSlots?.Length ?? -1);
            if (planner?.Agents != null)
            {
                for (int agentIndex = 0; agentIndex < planner.Agents.Count; agentIndex++)
                {
                    CharacterTestRuntimeAiPlannerAgentSnapshot agent = planner.Agents[agentIndex];
                    plannerDiagnostic += " [entity=" + agent.EntityId
                        + " profile=" + agent.RuntimeAiPlannerProfileSlot
                        + " loadout=" + agent.RuntimeAiBehaviorLoadoutId + "]";
                }
            }
            if (plannerState?.BehaviorLogicSlots != null)
            {
                for (int logicIndex = 0; logicIndex < plannerState.BehaviorLogicSlots.Length; logicIndex++)
                {
                    CharacterTestRuntimeAiLabLogicSlotStateSnapshot slot = plannerState.BehaviorLogicSlots[logicIndex];
                    plannerDiagnostic += " [logicSlot=" + slot.Slot
                        + " agents=" + (slot.Agents?.Length ?? -1) + "]";
                    if (slot.Agents != null)
                    {
                        for (int stateAgentIndex = 0; stateAgentIndex < slot.Agents.Length; stateAgentIndex++)
                        {
                            CharacterTestRuntimeAiLabLogicAgentStateSnapshot stateAgent = slot.Agents[stateAgentIndex];
                            plannerDiagnostic += " [logicEntity=" + stateAgent.EntityId
                                + " active=" + stateAgent.ActiveBindingIndex
                                + " stage=" + stateAgent.CurrentStageIndex
                                + " entered=" + stateAgent.StageEnteredFrame + "]";
                        }
                    }
                }
            }
            CharacterTestRuntimeAiLabLogicEventBuffer latestLogicEvents =
                controller.LatestBehaviorLogicEventsForLab;
            plannerDiagnostic += " latestLogicEvents=" + (latestLogicEvents?.Count ?? -1);
            if (latestLogicEvents != null)
            {
                for (int logicEventIndex = 0; logicEventIndex < latestLogicEvents.Count; logicEventIndex++)
                {
                    CharacterTestRuntimeAiLabLogicEvent logicEvent = latestLogicEvents[logicEventIndex];
                    plannerDiagnostic += " [eventFrame=" + logicEvent.Frame
                        + " kind=" + logicEvent.Kind
                        + " stage=" + logicEvent.StageId + "]";
                }
            }
            for (int trackIndex = 0; trackIndex < projection.Tracks.Count && !foundStageMarker; trackIndex++)
            {
                CharacterTestRuntimeAiLabTimelineTrackProjection track = projection.Tracks[trackIndex];
                for (int itemIndex = 0; itemIndex < track.Items.Count; itemIndex++)
                {
                    CharacterTestRuntimeAiLabTimelineItem item = track.Items[itemIndex];
                    observedItems.Add(track.Kind + ":" + item.Label + ":" + item.Category);
                    if (string.Equals(
                            item.Category,
                            "BehaviorLogic.StageEntered",
                            StringComparison.Ordinal))
                    {
                        foundStageMarker = true;
                        break;
                    }
                    for (int memberIndex = 0; memberIndex < item.Members.Count; memberIndex++)
                    {
                        CharacterTestRuntimeAiLabTimelineItem member = item.Members[memberIndex];
                        observedItems.Add(track.Kind + ":member:" + member.Label + ":" + member.Category);
                        if (string.Equals(
                                member.Category,
                                "BehaviorLogic.StageEntered",
                                StringComparison.Ordinal))
                        {
                            foundStageMarker = true;
                            break;
                        }
                    }
                }
            }
            Assert.IsTrue(
                foundStageMarker,
                "The applied v2 card must emit a typed stage-enter marker on the real frame timeline. Observed: "
                + string.Join(" | ", observedItems)
                + plannerDiagnostic
                + " indexedStageMarkerFrame=" + indexedStageMarkerFrame
                + " timelineLatest=" + controller.Timeline.LatestFrame);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_SharedBehaviorGroupInstallsBaselineForEveryRemappedProfileSlot()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            Assert.IsTrue(
                controller.TryCreateReactiveChaseDodgeGroup(0, out string createMessage),
                createMessage);
            Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft sharedGroup));
            CharacterTestRuntimeAiLabDraftSlot sourceSlot = controller.DraftWorkspace.TryGetSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0);
            Assert.IsNotNull(sourceSlot);

            Assert.IsTrue(controller.SelectSetupSpawnSlot(CharacterTestArenaMatchConfig.Team2Id, 0));
            Assert.IsTrue(controller.SelectSetupBehaviorGroupWorkspaceDraft(
                sharedGroup.GroupDraftId,
                sourceSlot.ArenaBehaviorGroupDraftId));
            controller.DraftWorkspace.RestoreSetupToArena(controller.MatchConfig);
            Assert.AreEqual(
                CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                    controller.MatchConfig.Team1Slots[0].BehaviorGroupDraftId),
                CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                    controller.MatchConfig.Team2Slots[0].BehaviorGroupDraftId),
                "The regression requires both enabled characters to reference one runtime behavior loadout.");
            Assert.IsTrue(controller.SelectSetupSpawnSlot(CharacterTestArenaMatchConfig.Team1Id, 0));
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerStateSnapshot plannerState =
                controller.CaptureRuntimeAiPlannerStateForLab();
            CharacterTestRuntimeAiPlannerSnapshot planner =
                controller.CaptureRuntimeAiPlannerSnapshotForLab();
            Assert.IsNotNull(plannerState);
            Assert.IsNotNull(plannerState.BehaviorLogicSlots);
            Assert.AreEqual(
                2,
                plannerState.BehaviorLogicSlots.Length,
                "Player Deck isolation may remap the player, but every remapped slot using the same group needs the same baseline overlay.");

            int firstProfileSlot = -1;
            int firstEntityId = 0;
            for (int i = 0; i < plannerState.BehaviorLogicSlots.Length; i++)
            {
                var logicSlot = plannerState.BehaviorLogicSlots[i];
                Assert.AreEqual(1, logicSlot.Agents.Length);
                Assert.AreEqual(controller.CandidateRevision.AuthorityHash, logicSlot.AuthorityHash);
                Assert.AreEqual(
                    controller.CandidateRevision.LogicPrograms[0].SemanticHash,
                    logicSlot.Agents[0].ProgramSemanticHash);
                if (i == 0)
                {
                    firstProfileSlot = logicSlot.Slot;
                    firstEntityId = logicSlot.Agents[0].EntityId;
                }
                else
                {
                    Assert.AreNotEqual(firstProfileSlot, logicSlot.Slot);
                    Assert.AreNotEqual(firstEntityId, logicSlot.Agents[0].EntityId);
                }
            }

            Assert.IsNotNull(planner);
            Assert.AreEqual(2, planner.Agents.Count);
            Assert.AreEqual(
                planner.Agents[0].RuntimeAiBehaviorLoadoutId,
                planner.Agents[1].RuntimeAiBehaviorLoadoutId);
            Assert.AreNotEqual(
                planner.Agents[0].RuntimeAiPlannerProfileSlot,
                planner.Agents[1].RuntimeAiPlannerProfileSlot,
                "The fix must preserve the player/enemy profile-slot isolation contract.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_HistoricalLogicFrameRestoresStageDetectorAndCommittedPath()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            Assert.IsTrue(controller.TryCreateReactiveChaseDodgeGroup(0, out string createMessage), createMessage);
            Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft historicalGroup));
            int historicalLogicEntryIndex = -1;
            for (int entryIndex = 0; entryIndex < historicalGroup.Entries.Count; entryIndex++)
            {
                if (!string.Equals(
                        historicalGroup.Entries[entryIndex].BehaviorId,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                        StringComparison.Ordinal))
                    continue;
                historicalLogicEntryIndex = entryIndex;
                break;
            }
            Assert.GreaterOrEqual(historicalLogicEntryIndex, 0);
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "logic.observe"
            };
            payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = "logic.observe",
                Order = 0,
                Kind = CharacterTestRuntimeAiLabCardStepKind.Logic,
                Title = "Observe"
            });
            payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = "logic.wait",
                Order = 1,
                Kind = CharacterTestRuntimeAiLabCardStepKind.Logic,
                Title = "Wait"
            });
            payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = "logic.damage",
                Order = 2,
                Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                Title = "Damage Output",
                CharacterActionId = CharacterTestCharacterActionConfigSource.LightAttackActionId
            });
            var detector = new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = "detector.target",
                Title = "Target Exists",
                Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                StageId = "logic.observe",
                Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                Priority = 100,
                ActivationStartFrame = 30,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite
            };
            detector.Conditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "detector.target.condition",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true
            });
            payload.Detectors.Add(detector);
            var outputGuard = new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = "detector.output-guard",
                Title = "Action Ready",
                Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                StageId = "logic.wait",
                Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                Priority = 10,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite
            };
            outputGuard.Conditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "detector.output-guard.condition",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true,
                CharacterActionId = CharacterTestCharacterActionConfigSource.LightAttackActionId
            });
            payload.Detectors.Add(outputGuard);
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "transition.observe-to-wait",
                DetectorId = detector.DetectorId,
                SourceStageId = "logic.observe",
                TargetStageId = "logic.wait",
                Priority = detector.Priority,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.PreserveAction,
                Label = "Target acquired"
            });
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "transition.wait-to-damage",
                DetectorId = outputGuard.DetectorId,
                SourceStageId = "logic.wait",
                TargetStageId = "logic.damage",
                Priority = outputGuard.Priority,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt,
                Label = "Action ready"
            });
            payload.Layout.Stages.Add(new CharacterTestRuntimeAiLabLogicStageLayout
            {
                StageId = "logic.observe",
                Column = 0,
                Width = 1f
            });
            payload.Layout.Stages.Add(new CharacterTestRuntimeAiLabLogicStageLayout
            {
                StageId = "logic.wait",
                Column = 1,
                Width = 1f
            });
            payload.Layout.Stages.Add(new CharacterTestRuntimeAiLabLogicStageLayout
            {
                StageId = "logic.damage",
                Column = 2,
                Width = 1f
            });
            Assert.IsTrue(controller.SetSetupSpawnPosition(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                new Vector2(-2.5f, 0f)));
            Assert.IsTrue(controller.SetSetupSpawnPosition(
                CharacterTestArenaMatchConfig.Team2Id,
                0,
                new Vector2(2.5f, 0f)));
            Assert.IsTrue(controller.SelectSetupSpawnSlot(CharacterTestArenaMatchConfig.Team1Id, 0));
            Assert.IsTrue(
                controller.TryReplaceSelectedCardLogic(
                    historicalLogicEntryIndex,
                    payload,
                    out string replaceMessage),
                replaceMessage);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabCardStepKind.Logic,
                controller.CandidateRevision.LogicPrograms[historicalLogicEntryIndex].Stages[0].Kind);
            Assert.IsFalse(
                controller.CandidateRevision.Profile.Entries[historicalLogicEntryIndex].Disabled,
                "The damage core slot must remain enabled after its behavior logic is edited.");
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerStateSnapshot startedPlannerState =
                controller.CaptureRuntimeAiPlannerStateForLab();
            Assert.IsNotNull(startedPlannerState);
            Assert.IsNotNull(startedPlannerState.BehaviorLogicSlots);
            Assert.AreEqual(1, startedPlannerState.BehaviorLogicSlots.Length);
            Assert.AreEqual(1, startedPlannerState.BehaviorLogicSlots[0].Agents.Length);
            CharacterTestRuntimeAiPlannerSnapshot startedPlanner =
                controller.CaptureRuntimeAiPlannerSnapshotForLab();
            Assert.GreaterOrEqual(controller.SelectedAgentIndex, 0);
            Assert.Less(controller.SelectedAgentIndex, startedPlanner.Agents.Count);
            Assert.AreEqual(
                startedPlannerState.BehaviorLogicSlots[0].Slot,
                startedPlanner.Agents[controller.SelectedAgentIndex].RuntimeAiPlannerProfileSlot,
                "The default observed Agent must own the behavior-card baseline.");
            int startedLogicEntityId = startedPlannerState.BehaviorLogicSlots[0].Agents[0].EntityId;
            bool foundStartedLogicAgent = false;
            for (int agentIndex = 0;
                 startedPlanner?.Agents != null && agentIndex < startedPlanner.Agents.Count;
                 agentIndex++)
            {
                CharacterTestRuntimeAiPlannerAgentSnapshot startedAgent =
                    startedPlanner.Agents[agentIndex];
                if (startedAgent.EntityId != startedLogicEntityId)
                    continue;

                foundStartedLogicAgent = true;
                Assert.AreEqual(
                    startedAgent.RuntimeAiPlannerProfileSlot,
                    startedPlannerState.BehaviorLogicSlots[0].Slot,
                    "The session baseline overlay must use the selected agent's remapped profile slot.");
                break;
            }
            Assert.IsTrue(foundStartedLogicAgent, "The behavior logic owner must exist in the planner snapshot.");
            Assert.AreEqual(
                controller.CandidateRevision.AuthorityHash,
                startedPlannerState.BehaviorLogicSlots[0].AuthorityHash,
                "The historical-frame scenario must inspect the candidate overlay, not a stale profile slot.");
            Assert.AreEqual(
                CharacterTestRuntimeAiLabCardStepKind.Logic,
                controller.CandidateRevision.LogicPrograms[historicalLogicEntryIndex].Stages[0].Kind);
            Assert.AreEqual(
                controller.CandidateRevision.LogicPrograms[historicalLogicEntryIndex].SemanticHash,
                startedPlannerState.BehaviorLogicSlots[0].Agents[0].ProgramSemanticHash,
                "The historical-frame scenario must exercise the edited behavior card. active="
                + startedPlannerState.BehaviorLogicSlots[0].Agents[0].ActiveBindingIndex
                + " hashes=" + controller.CandidateRevision.LogicPrograms[0].SemanticHash
                + "," + controller.CandidateRevision.LogicPrograms[1].SemanticHash
                + "," + controller.CandidateRevision.LogicPrograms[2].SemanticHash);

            int committedFrame = -1;
            int committedEntityId = 0;
            int committedProfileSlot = -1;
            int committedCardInstanceStartFrame = -1;
            string committedBehaviorEntryId = string.Empty;
            long committedProgramSemanticHash = 0L;
            Assert.IsTrue(controller.StepFrames(120), controller.StatusMessage);
            for (int frame = controller.Timeline.RetainedStartFrame;
                 frame <= controller.Timeline.LatestFrame && committedFrame < 0;
                 frame++)
            {
                CharacterTestRuntimeAiLabLogicEventBuffer events =
                    controller.GetBehaviorLogicEventsForFrame(frame);
                if (events == null)
                    continue;
                for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                {
                    if (events[eventIndex].Kind == CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted
                        && string.Equals(
                            events[eventIndex].TransitionId,
                            "transition.observe-to-wait",
                            StringComparison.Ordinal))
                    {
                        committedFrame = events[eventIndex].Frame;
                        committedEntityId = events[eventIndex].EntityId;
                        committedProfileSlot = events[eventIndex].ProfileSlot;
                        committedCardInstanceStartFrame = events[eventIndex].CardInstanceStartFrame;
                        committedBehaviorEntryId = events[eventIndex].BehaviorEntryId;
                        committedProgramSemanticHash = events[eventIndex].ProgramSemanticHash;
                        break;
                    }
                }
            }
            CharacterTestRuntimeAiPlannerStateSnapshot unresolvedState =
                controller.CaptureRuntimeAiPlannerStateForLab();
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot unresolvedAgent =
                unresolvedState.BehaviorLogicSlots[0].Agents[0];
            Assert.GreaterOrEqual(
                committedFrame,
                0,
                "The detector must commit the stage jump on a RuntimeFrame. stage="
                + unresolvedAgent.CurrentStageIndex
                + " runtimeState=" + unresolvedAgent.RuntimeState
                + " actionState=" + unresolvedAgent.PrimaryActionState
                + " entered=" + unresolvedAgent.StageEnteredFrame
                + " actionEntered=" + unresolvedAgent.ActionEnteredFrame
                + " timelineLatest=" + controller.Timeline.LatestFrame);
            CharacterTestRuntimeAiLabLogicEventBuffer indexedCommittedEvents =
                controller.GetBehaviorLogicEventsForFrame(committedFrame);
            bool indexedCommittedPath = false;
            for (int eventIndex = 0; eventIndex < indexedCommittedEvents.Count; eventIndex++)
            {
                if (indexedCommittedEvents[eventIndex].Kind == CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted
                    && string.Equals(
                        indexedCommittedEvents[eventIndex].TransitionId,
                        "transition.observe-to-wait",
                        StringComparison.Ordinal))
                {
                    indexedCommittedPath = true;
                    break;
                }
            }
            Assert.IsTrue(indexedCommittedPath, "The typed timeline index must retain the committed path on its exact RuntimeFrame.");
            Assert.AreEqual(
                startedPlannerState.BehaviorLogicSlots[0].Slot,
                committedProfileSlot,
                "Timeline events must preserve their profile-slot provenance.");
            Assert.GreaterOrEqual(
                committedCardInstanceStartFrame,
                0,
                "Timeline events must preserve the behavior-card runtime-instance boundary.");
            Assert.LessOrEqual(
                committedCardInstanceStartFrame,
                committedFrame,
                "A committed event cannot precede its behavior-card runtime instance.");
            Assert.IsNotEmpty(
                committedBehaviorEntryId,
                "Timeline events must preserve the committed behavior-entry identity.");
            Assert.AreEqual(
                startedPlannerState.BehaviorLogicSlots[0].Agents[0].ProgramSemanticHash,
                committedProgramSemanticHash,
                "Timeline events must preserve the frozen program semantic identity.");
            Assert.IsTrue(controller.StepFrame(), controller.StatusMessage);
            Assert.Greater(controller.Timeline.LatestFrame, committedFrame);
            Assert.IsTrue(controller.JumpToFrame(committedFrame), controller.StatusMessage);
            Assert.IsFalse(
                controller.CanEditBehaviorCardDraft,
                "The selected historical RuntimeFrame must close the Draft boundary before any ViewModel refresh.");
            CharacterTestRuntimeAiPlannerSnapshot historicalPlanner =
                controller.CaptureRuntimeAiPlannerSnapshotForLab();
            CharacterTestRuntimeAiPlannerStateSnapshot historicalPlannerState =
                controller.CaptureRuntimeAiPlannerStateForLab();
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot historicalLogicState = null;
            for (int slotIndex = 0;
                 slotIndex < historicalPlannerState.BehaviorLogicSlots.Length;
                 slotIndex++)
            {
                CharacterTestRuntimeAiLabLogicSlotStateSnapshot slot =
                    historicalPlannerState.BehaviorLogicSlots[slotIndex];
                if (slot.Slot != committedProfileSlot)
                    continue;
                for (int agentIndex = 0; agentIndex < slot.Agents.Length; agentIndex++)
                {
                    if (slot.Agents[agentIndex].EntityId == committedEntityId)
                    {
                        historicalLogicState = slot.Agents[agentIndex];
                        break;
                    }
                }
            }
            Assert.IsNotNull(
                historicalLogicState,
                "Historical behavior timeline state is missing after a successful seek. slots="
                + (historicalPlannerState.BehaviorLogicSlots?.Length ?? -1)
                + ", expectedSlot="
                + committedProfileSlot
                + ", expectedEntity="
                + committedEntityId
                + ", observed="
                + string.Join(
                    ";",
                    (historicalPlannerState.BehaviorLogicSlots
                        ?? Array.Empty<CharacterTestRuntimeAiLabLogicSlotStateSnapshot>())
                    .Select(slot => slot.Slot
                        + ":"
                        + (slot.Agents?.Length ?? -1)
                        + "["
                        + string.Join(",", (slot.Agents
                            ?? Array.Empty<CharacterTestRuntimeAiLabLogicAgentStateSnapshot>())
                            .Select(agent => agent.EntityId))
                        + "]")));
            string snapshotCurrentStageId =
                controller.CandidateRevision.LogicPrograms[historicalLogicEntryIndex]
                .Stages[historicalLogicState.CurrentStageIndex].StageId;
            int selectedAgentIndex = -1;
            for (int agentIndex = 0; agentIndex < historicalPlanner.Agents.Count; agentIndex++)
            {
                if (historicalPlanner.Agents[agentIndex].EntityId == committedEntityId)
                {
                    Assert.IsTrue(controller.SelectAgent(agentIndex), "The historical event owner must remain selectable.");
                    selectedAgentIndex = agentIndex;
                    break;
                }
            }
            Assert.GreaterOrEqual(selectedAgentIndex, 0);
            if (controller.AuthoringMode != CharacterTestRuntimeAiLabAuthoringMode.Developer)
                controller.ToggleAuthoringMode();

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel historical =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                    controller,
                    commandStatus: null,
                    workspaceMode: CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                    selectedWorkbenchEntryIndex: historicalLogicEntryIndex,
                    selectedCompiledStepIndex: 1,
                    selectedLogicElementKind: CharacterTestRuntimeAiLabLogicElementKind.Transition,
                    selectedLogicElementIndex: 0);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel historicalTimeline =
                historical.BehaviorCardWorkspace.Timeline;
            Assert.IsTrue(historicalTimeline.Available, "Historical logical timeline must be available.");
            CharacterTestRuntimeAiLabLogicStageViewModel historicalCurrent = null;
            for (int stageIndex = 0; stageIndex < historicalTimeline.StageCount; stageIndex++)
            {
                if (string.Equals(
                        historicalTimeline.Stages[stageIndex].StageId,
                        snapshotCurrentStageId,
                        StringComparison.Ordinal))
                {
                    historicalCurrent = historicalTimeline.Stages[stageIndex];
                }
            }
            Assert.IsNotNull(
                historicalCurrent,
                "Historical current Stage is absent from the legacy projection. stage="
                + snapshotCurrentStageId
                + ", stageCount="
                + historicalTimeline.StageCount);
            Assert.IsTrue(
                historicalCurrent.Current,
                "Historical current Stage must come from the selected-frame snapshot; the typed commit only emphasizes its path. "
                + historicalTimeline.RuntimeStatusText
                + " committedFrame=" + committedFrame
                + " selectedFrame=" + controller.Timeline.SelectedFrame);
            CharacterTestRuntimeAiLabLogicDetectorViewModel historicalDetector = null;
            for (int detectorIndex = 0; detectorIndex < historicalTimeline.DetectorCount; detectorIndex++)
            {
                if (string.Equals(
                    historicalTimeline.Detectors[detectorIndex].DetectorId,
                    "event.transition.observe-to-wait",
                    StringComparison.Ordinal))
                {
                    historicalDetector = historicalTimeline.Detectors[detectorIndex];
                }
            }
            Assert.IsNotNull(
                historicalDetector,
                "Historical detector is absent from the legacy projection. detectorCount="
                + historicalTimeline.DetectorCount);
            CharacterTestRuntimeAiLabLogicTransitionViewModel historicalTransition = null;
            for (int transitionIndex = 0; transitionIndex < historicalTimeline.TransitionCount; transitionIndex++)
            {
                if (string.Equals(
                    historicalTimeline.Transitions[transitionIndex].TransitionId,
                    "transition.observe-to-wait",
                    StringComparison.Ordinal))
                {
                    historicalTransition = historicalTimeline.Transitions[transitionIndex];
                }
            }
            Assert.IsNotNull(
                historicalTransition,
                "Historical transition is absent from the legacy projection. transitionCount="
                + historicalTimeline.TransitionCount);
            Assert.IsTrue(historicalTransition.Committed, "Historical typed marker must mark the actual path committed.");
            Assert.AreEqual(committedFrame, historicalTransition.RuntimeFrame);

            var recipeProjection = new BehaviorRecipeTimelineProjection();
            recipeProjection.Rebuild(historicalTimeline);
            BehaviorRecipeTimelineViewModel historicalCurrentRecipeTimeline = null;
            for (int timelineIndex = 0; timelineIndex < recipeProjection.TimelineCount; timelineIndex++)
            {
                BehaviorRecipeTimelineViewModel candidate = recipeProjection.GetTimeline(timelineIndex);
                if (string.Equals(candidate?.StageId, snapshotCurrentStageId, StringComparison.Ordinal))
                {
                    historicalCurrentRecipeTimeline = candidate;
                    break;
                }
            }
            Assert.IsNotNull(historicalCurrentRecipeTimeline, "Historical v5 Timeline must remain projected after seek.");
            Assert.IsTrue(
                historicalCurrentRecipeTimeline.Current,
                "The selected historical frame must mark the restored current v5 Timeline.");
            BehaviorRecipeEventViewModel committedEvent = null;
            for (int timelineIndex = 0;
                 timelineIndex < recipeProjection.TimelineCount && committedEvent == null;
                 timelineIndex++)
            {
                BehaviorRecipeTimelineViewModel candidateTimeline = recipeProjection.GetTimeline(timelineIndex);
                for (int trackIndex = 0; trackIndex < candidateTimeline.TrackCount; trackIndex++)
                {
                    BehaviorRecipeEventViewModel candidate = candidateTimeline.GetTrack(trackIndex)?.Event;
                    if (candidate?.Kind == BehaviorRecipeEventKind.Branch
                        && string.Equals(
                            candidate.DetectorId,
                            "event.transition.observe-to-wait",
                            StringComparison.Ordinal))
                    {
                        committedEvent = candidate;
                        break;
                    }
                }
            }
            Assert.IsNotNull(committedEvent, "The committed v5 Trigger Event must remain on its Branch Track.");
            Assert.GreaterOrEqual(committedEvent.EffectiveTransitionIndex, 0);
            Assert.AreEqual(
                "transition.observe-to-wait",
                historicalTimeline.Transitions[committedEvent.EffectiveTransitionIndex].TransitionId);

            yield return new ExitPlayMode();
        }
    }
}
