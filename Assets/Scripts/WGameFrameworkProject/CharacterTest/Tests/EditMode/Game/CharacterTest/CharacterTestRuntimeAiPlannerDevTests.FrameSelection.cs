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

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_SaveBehaviorRecipeAndRunInstallsVerifiedOverlayAndPlays()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller, "Runtime AI Lab scene controller must exist.");
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            Assert.IsTrue(controller.SelectSetupSpawnSlot(CharacterTestArenaMatchConfig.Team1Id, 0));
            Assert.IsTrue(
                controller.TryCreateReactiveChaseDodgeGroup(0, out string createMessage),
                createMessage);
            Assert.IsTrue(controller.DraftWorkspace.Dirty);

            Assert.IsTrue(
                controller.QueueBehaviorRecipeSaveAndRun(
                    selectedEntryIndex: 0,
                    out string queuedMessage),
                queuedMessage);
            Assert.IsFalse(
                controller.CanSaveBehaviorRecipeAndRun,
                "The Save button must disable as soon as the next-frame transaction is queued.");
            int saveFrameBudget = 5;
            while (controller.BehaviorRecipeSaveQueued && saveFrameBudget-- > 0)
                yield return null;
            Assert.IsFalse(
                controller.BehaviorRecipeSaveQueued,
                "The queued Save transaction must run after the input callback frame.");

            CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult result =
                controller.LastBehaviorRecipeSaveRun;
            Assert.IsTrue(result.Success, result.Message);
            Assert.That(
                result.Status,
                Is.EqualTo(CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedAndRunning));
            Assert.That(result.CandidateAuthorityHash, Is.Not.EqualTo(0L));
            Assert.That(result.InstalledAuthorityHash, Is.EqualTo(result.CandidateAuthorityHash));
            Assert.That(result.SelectedEntityId, Is.Not.EqualTo(0));
            Assert.That(controller.ActiveRevision, Is.Not.Null);
            Assert.That(
                controller.BehaviorRecipeSyncState,
                Is.EqualTo(CharacterTestRuntimeAiLabBehaviorRecipeSyncState.RunningCurrentDraft));

            CharacterTestRuntimeAiPlannerDevSceneSnapshot started = controller.CaptureSnapshot();
            Assert.That(started.Phase, Is.EqualTo(CharacterTestRuntimeAiPlannerDevScenePhase.Running));
            Assert.That(started.Paused, Is.False);
            Assert.That(started.SelectedFrame, Is.Not.Null);
            int startFrame = started.SelectedFrame.Frame;

            Assert.IsFalse(
                controller.SaveBehaviorRecipeAndRun(
                    selectedEntryIndex: 0,
                    out CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult rerun),
                "没有修改时不允许再次保存。");
            Assert.That(rerun.FailureCode, Is.EqualTo("NO_UNSAVED_CHANGES"));

            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneSnapshot advanced = controller.CaptureSnapshot();
            Assert.That(advanced.Paused, Is.False);
            Assert.That(advanced.SelectedFrame.Frame, Is.GreaterThan(startFrame));

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_SaveRecomputesToPreviousFrameAndPreservesPauseState()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            Assert.IsTrue(controller.SelectSetupSpawnSlot(CharacterTestArenaMatchConfig.Team1Id, 0));
            Assert.IsTrue(
                controller.TryCreateReactiveChaseDodgeGroup(0, out string createMessage),
                createMessage);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            Assert.IsTrue(controller.TogglePause(), controller.StatusMessage);

            int frameBudget = 20;
            while (controller.CaptureSnapshot().SelectedFrame.Frame < 12 && frameBudget-- > 0)
                yield return null;

            Assert.IsTrue(controller.TogglePause(), controller.StatusMessage);
            CharacterTestRuntimeAiPlannerDevSceneSnapshot beforeSave = controller.CaptureSnapshot();
            Assert.IsTrue(beforeSave.Paused);
            int frameBeforeSave = beforeSave.SelectedFrame.Frame;
            Assert.Greater(frameBeforeSave, 3);

            Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft group));
            int editableEntryIndex = group.Entries.Count - 1;
            Assert.IsTrue(
                controller.TryResolveStageEditIdentity(
                    editableEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity identity,
                    out string identityMessage),
                identityMessage);
            CharacterTestRuntimeAiLabBehaviorCardDraft card =
                controller.DraftWorkspace.Cards[group.Entries[editableEntryIndex].CardDraftId];
            string stageId = card.LogicPayload.Stages[0].StageId;
            Assert.IsTrue(
                controller.TryEditLogicStageField(
                    editableEntryIndex,
                    identity,
                    stageId,
                    CharacterTestRuntimeAiLabStageFieldKind.Note,
                    new CharacterTestRuntimeAiLabStageFieldValue(
                        stringValue: "保存重算测试"),
                    out _,
                    out string editMessage),
                editMessage);
            Assert.IsTrue(controller.DraftWorkspace.Dirty);
            Assert.IsTrue(
                controller.SaveBehaviorRecipeAndRun(
                    selectedEntryIndex: editableEntryIndex,
                    out CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult saved),
                saved.Message);

            CharacterTestRuntimeAiPlannerDevSceneSnapshot afterSave = controller.CaptureSnapshot();
            Assert.That(afterSave.SelectedFrame.Frame, Is.EqualTo(frameBeforeSave));
            Assert.IsTrue(afterSave.Paused, "保存前暂停时，重算回原帧后仍应暂停。");
            Assert.IsFalse(controller.DraftWorkspace.Dirty);
            Assert.IsFalse(controller.CanSaveBehaviorRecipeAndRun);
            StringAssert.Contains("重新模拟到保存前的帧", saved.Message);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_SaveBehaviorRecipeAndRunRejectsInvalidDraftBeforePersistence()
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
                out CharacterTestRuntimeAiLabBehaviorGroupDraft editableGroup));
            int editableEntryIndex = -1;
            for (int i = 0; i < editableGroup.Entries.Count; i++)
            {
                if (string.Equals(
                        editableGroup.Entries[i].BehaviorId,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                        StringComparison.Ordinal))
                {
                    editableEntryIndex = i;
                    break;
                }
            }
            Assert.That(editableEntryIndex, Is.GreaterThanOrEqualTo(0));
            CharacterTestRuntimeAiLabBehaviorCardPayload invalid =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture();
            invalid.Steps[0].AbilityId = 999999;
            Assert.IsTrue(
                controller.DraftWorkspace.TryReplaceCardPayload(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    editableEntryIndex,
                    invalid,
                    out string replaceMessage),
                replaceMessage);

            Assert.IsFalse(
                controller.SaveBehaviorRecipeAndRun(
                    selectedEntryIndex: editableEntryIndex,
                    out CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult result));
            Assert.That(
                result.Status,
                Is.EqualTo(CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SaveFailed));
            Assert.That(controller.DraftWorkspace.Dirty, Is.True);
            Assert.IsTrue(
                controller.HasBehaviorRecipeChangesToSave,
                "Rejected invalid authoring must remain dirty so the user can correct or undo it.");
            Assert.That(controller.Phase, Is.EqualTo(CharacterTestRuntimeAiPlannerDevScenePhase.Setup));
            Assert.That(controller.ActiveRevision, Is.Null);
            StringAssert.Contains("未保存", result.Message);
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_TrainingDummyStartUsesOnlyAuthoredBehaviors()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller, "Runtime AI Lab scene controller must exist.");
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            Assert.IsTrue(controller.SelectSetupSpawnSlot(CharacterTestArenaMatchConfig.Team2Id, 0));
            Assert.IsTrue(controller.SelectSetupBehaviorGroupDraft(4));
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            yield return null;

            Assert.AreEqual(CharacterTestRuntimeAiLabCandidateState.Applied, controller.CandidateState);
            Assert.IsNotNull(controller.ActiveRevision);
            Assert.AreEqual(2, controller.ActiveRevision.Profile.Entries.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle
                },
                controller.ActiveRevision.Profile.Entries
                    .Select(entry => entry.BehaviorId)
                    .ToArray());
            Assert.AreEqual(2, controller.ActiveRevision.LogicPrograms.Count);
            Assert.IsTrue(controller.ActiveRevision.LogicPrograms.All(program =>
                program.Stages.All(stage =>
                    stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Logic
                    || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait)));
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_StepForwardBackForwardKeepsFrameSync()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(controller, "Runtime AI Lab scene controller must exist.");
            Assert.IsNotNull(manager, "Runtime AI Lab scene GameManager must exist.");
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            yield return null;

            Assert.IsTrue(manager.RuntimeContext.Paused, "Runtime AI Lab should start in paused inspect mode.");
            CharacterTestRuntimeAiLabFrameSyncSample initial =
                CaptureSceneControllerFrameSyncSample(controller, manager);
            CharacterTestRuntimeAiLabAuthoritySample initialAuthority =
                CaptureSceneControllerAuthoritySample(manager);
            Assert.IsFalse(controller.StateCaptureEnabled);
            for (int i = 0; i < controller.Timeline.Count; i++)
            {
                Assert.IsFalse(
                    controller.Timeline.Records[i].HasCheckpoint,
                    "Default Frame Sync replay must not create automatic intermediate checkpoints.");
            }
            Assert.IsTrue(controller.StepForward10(), controller.StatusMessage);
            CharacterTestRuntimeAiLabFrameSyncSample firstForward =
                CaptureSceneControllerFrameSyncSample(controller, manager);
            CharacterTestRuntimeAiLabAuthoritySample firstForwardAuthority =
                CaptureSceneControllerAuthoritySample(manager);

            Assert.IsTrue(controller.StepBack10(), controller.StatusMessage);
            int firstRestoreBudget = 600;
            while (controller.ReplayInProgress && firstRestoreBudget-- > 0)
                yield return null;
            Assert.IsFalse(controller.ReplayInProgress, controller.StatusMessage);
            Assert.Greater(firstRestoreBudget, 0, "The first historical restore did not complete in time.");
            StringAssert.DoesNotContain("fallback", controller.StatusMessage.ToLowerInvariant());
            CharacterTestRuntimeAiLabFrameSyncSample restoredInitial =
                CaptureSceneControllerFrameSyncSample(controller, manager);
            CharacterTestRuntimeAiLabAuthoritySample restoredInitialAuthority =
                CaptureSceneControllerAuthoritySample(manager);
            CharacterTestRuntimeAiLabFrameSyncReport backwardReport =
                CharacterTestRuntimeAiLabAuthorityProbe.Compare(
                    initialAuthority,
                    restoredInitialAuthority);
            Assert.IsTrue(controller.StepForward10(), controller.StatusMessage);
            CharacterTestRuntimeAiLabFrameSyncSample secondForward =
                CaptureSceneControllerFrameSyncSample(controller, manager);
            CharacterTestRuntimeAiLabAuthoritySample secondForwardAuthority =
                CaptureSceneControllerAuthoritySample(manager);
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabAuthorityProbe.Compare(
                    firstForwardAuthority,
                    secondForwardAuthority);
            CharacterTestRuntimeAiLabFrameSyncReport forwardPresentationReport =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(firstForward, secondForward);

            int latestFrame = controller.CaptureSnapshot().Timeline.LatestFrame;
            Assert.IsTrue(controller.StepBack10(), controller.StatusMessage);
            int secondRestoreBudget = 600;
            while (controller.ReplayInProgress && secondRestoreBudget-- > 0)
                yield return null;
            Assert.IsFalse(controller.ReplayInProgress, controller.StatusMessage);
            Assert.Greater(secondRestoreBudget, 0, "The second historical restore did not complete in time.");
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerDevPresentationContext.Review,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.ResolvePresentationContext(
                    controller.CaptureSnapshot()));
            Assert.IsTrue(controller.TogglePause(), controller.StatusMessage);
            int historyPlaybackTimeout = 120;
            while (controller.HistoryPlaybackActive && historyPlaybackTimeout-- > 0)
                yield return null;
            Assert.IsFalse(
                controller.HistoryPlaybackActive,
                "History playback did not finish before the timeout. " + controller.StatusMessage);
            Assert.IsFalse(
                manager.RuntimeContext.Paused,
                "History playback reached the latest frame but did not resume Live. " + controller.StatusMessage);
            Assert.AreEqual(latestFrame, manager.RuntimeContext.Slice.CurrentFrame.Value);
            Assert.AreEqual(latestFrame, controller.CaptureSnapshot().Timeline.SelectedFrame);

            yield return new ExitPlayMode();

            Assert.IsTrue(backwardReport.Passed, backwardReport.SummaryText);
            Assert.IsTrue(report.Passed, report.SummaryText);
            for (int mismatchIndex = 0;
                 mismatchIndex < forwardPresentationReport.MismatchRows.Count;
                 mismatchIndex++)
            {
                CharacterTestRuntimeAiLabFrameSyncMismatch mismatch =
                    forwardPresentationReport.MismatchRows[mismatchIndex];
                Assert.IsFalse(
                    mismatch.Layer == "animation"
                    || mismatch.Layer == "animationInput"
                    || mismatch.Layer == "bonePose"
                    || mismatch.Layer == "procedural"
                    || mismatch.Layer == "view"
                    || mismatch.Layer == "overlay",
                    forwardPresentationReport.SummaryText);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_ManualStateCaptureValidateAndSyncAreExplicit()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            Assert.IsFalse(controller.StateCaptureEnabled);
            Assert.IsFalse(controller.HasSelectedTestStateCapture);

            Assert.IsTrue(controller.ToggleStateCapture(), controller.StatusMessage);
            yield return null;
            Assert.IsTrue(controller.StateCaptureEnabled);
            Assert.IsTrue(controller.HasSelectedTestStateCapture);
            Assert.IsTrue(controller.ValidateRecordedState(), controller.StatusMessage);
            StringAssert.Contains("帧同步：通过", controller.FrameSyncStatusText);
            Assert.IsTrue(controller.SyncRecordedState(), controller.StatusMessage);
            Assert.IsTrue(controller.ValidateRecordedState(), controller.StatusMessage);

            Assert.IsTrue(controller.ToggleStateCapture(), controller.StatusMessage);
            Assert.IsFalse(controller.StateCaptureEnabled);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_FutureScrubAdvancesSimulationAndCanRetreat()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(manager);
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            Assert.IsNotNull(
                manager.RuntimeContext.NetworkRunner,
                "Future-scrub regression coverage must exercise the Runtime AI Lab Loopback session.");

            int startFrame = controller.Timeline.LatestFrame;
            CharacterTestAnimationPresentationCheckpoint startPresentation =
                manager.RuntimeContext.Slice.CharacterRuntime
                    .CaptureAnimationPresentationCheckpoint(new RuntimeFrame(startFrame));
            Assert.Greater(
                startPresentation.Characters.Count,
                0,
                "Runtime AI Lab must expose at least one character presentation state.");
            int targetFrame = startFrame + 48;
            Assert.IsTrue(controller.BeginTimelineScrub(targetFrame), controller.StatusMessage);
            Assert.IsTrue(controller.EndTimelineScrub(targetFrame), controller.StatusMessage);
            int updateBudget = 120;
            while ((controller.ReplayInProgress || controller.FutureAdvanceActive)
                && updateBudget-- > 0)
                yield return null;
            Assert.Greater(updateBudget, 0, controller.StatusMessage);

            Assert.AreEqual(targetFrame, controller.Timeline.LatestFrame);
            Assert.AreEqual(targetFrame, controller.Timeline.SelectedFrame);
            Assert.AreEqual(targetFrame - startFrame + 1, controller.Timeline.Count);
            Assert.AreEqual(targetFrame, manager.RuntimeContext.Slice.CurrentFrame.Value);
            CharacterTestAnimationPresentationCheckpoint targetPresentation =
                manager.RuntimeContext.Slice.CharacterRuntime
                    .CaptureAnimationPresentationCheckpoint(new RuntimeFrame(targetFrame));
            Assert.AreEqual(
                startPresentation.Characters.Count,
                targetPresentation.Characters.Count,
                "Future scrub must preserve the active character presentation set.");
            Assert.IsTrue(
                HasAdvancedAnimationClock(startPresentation, targetPresentation),
                "Future scrub must advance the existing fixed-frame animation clock in Loopback sessions.");
            Assert.AreEqual(
                CharacterTestRuntimeAiLabFrameSelectionPhase.Idle,
                controller.CaptureSnapshot().FrameSelectionPhase,
                "A completed future scrub must commit the generated target and close its preview transaction.");

            Assert.IsTrue(controller.StepFrames(20), controller.StatusMessage);
            int latest = controller.Timeline.LatestFrame;
            int retreatFrame = latest - 8;
            Assert.IsTrue(controller.BeginTimelineScrub(retreatFrame), controller.StatusMessage);
            Assert.IsTrue(controller.EndTimelineScrub(retreatFrame), controller.StatusMessage);
            updateBudget = 120;
            while (controller.ReplayInProgress && updateBudget-- > 0)
                yield return null;
            Assert.Greater(updateBudget, 0, controller.StatusMessage);
            Assert.AreEqual(latest, controller.Timeline.LatestFrame, "回拖不能删除已生成帧。");
            Assert.AreEqual(retreatFrame, controller.Timeline.SelectedFrame, controller.StatusMessage);
            Assert.AreEqual(retreatFrame, manager.RuntimeContext.Slice.CurrentFrame.Value, controller.StatusMessage);
            Assert.IsTrue(manager.RuntimeContext.Paused);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_ContinuousFutureScrubCommitsEachDispatchedTarget()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);

            int startFrame = controller.Timeline.LatestFrame;
            int firstTargetFrame = startFrame + 8;
            int secondTargetFrame = startFrame + 16;

            Assert.IsTrue(controller.BeginTimelineScrub(firstTargetFrame), controller.StatusMessage);
            int firstCommittedProgress =
                controller.CaptureSnapshot().CommittedFrameReceipt.Frame;
            Assert.Greater(
                firstCommittedProgress,
                startFrame,
                "The blue committed playhead must advance as fully generated frames become available.");
            Assert.LessOrEqual(firstCommittedProgress, firstTargetFrame);
            int updateBudget = 120;
            while (controller.FutureAdvanceActive && updateBudget-- > 0)
                yield return null;
            Assert.Greater(updateBudget, 0, controller.StatusMessage);
            Assert.AreEqual(firstTargetFrame, controller.Timeline.LatestFrame);
            Assert.AreEqual(
                firstTargetFrame,
                controller.CaptureSnapshot().CommittedFrameReceipt.Frame,
                "The blue committed playhead must catch the first generated preview target.");
            Assert.AreEqual(
                firstTargetFrame,
                controller.BuildTimelineProjection().Domain.SelectedFrame);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabFrameSelectionPhase.Previewing,
                controller.CaptureSnapshot().FrameSelectionPhase);

            Assert.IsTrue(controller.UpdateTimelineScrub(secondTargetFrame), controller.StatusMessage);
            updateBudget = 120;
            int secondCommittedProgress = controller.CaptureSnapshot().CommittedFrameReceipt.Frame;
            while (secondCommittedProgress <= firstTargetFrame
                && controller.FutureAdvanceActive
                && updateBudget-- > 0)
            {
                yield return null;
                secondCommittedProgress = controller.CaptureSnapshot().CommittedFrameReceipt.Frame;
            }
            Assert.Greater(updateBudget, 0, controller.StatusMessage);
            Assert.Greater(
                secondCommittedProgress,
                firstTargetFrame,
                "The blue committed playhead must continue from the current drag, not the previous orange target.");
            Assert.LessOrEqual(secondCommittedProgress, secondTargetFrame);
            updateBudget = 120;
            while (controller.FutureAdvanceActive && updateBudget-- > 0)
                yield return null;
            Assert.Greater(updateBudget, 0, controller.StatusMessage);
            Assert.AreEqual(secondTargetFrame, controller.Timeline.LatestFrame);
            Assert.AreEqual(
                secondTargetFrame,
                controller.CaptureSnapshot().CommittedFrameReceipt.Frame,
                "A later pointer update must not leave the committed playhead one target behind.");
            Assert.AreEqual(
                secondTargetFrame,
                controller.BuildTimelineProjection().Domain.SelectedFrame);

            Assert.IsTrue(controller.EndTimelineScrub(secondTargetFrame), controller.StatusMessage);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabFrameSelectionPhase.Idle,
                controller.CaptureSnapshot().FrameSelectionPhase,
                "Pointer-up must close the scrub transaction even when the target was already generated.");
            Assert.AreEqual(
                secondTargetFrame,
                controller.CaptureSnapshot().CommittedFrameReceipt.Frame);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_LivePlaybackThenSeekRestoresAuthorityAndPresentation()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(manager);
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            Assert.IsTrue(controller.StartSessionThroughShell(), controller.StatusMessage);
            int firstFrame = controller.CaptureSnapshot().Timeline.SelectedFrame;
            Assert.IsTrue(controller.ToggleStateCapture(), controller.StatusMessage);

            Assert.IsTrue(controller.TogglePause(), controller.StatusMessage);
            for (int i = 0; i < 64; i++)
                yield return null;
            Assert.IsTrue(controller.TogglePause(), controller.StatusMessage);
            int latestFrame = controller.CaptureSnapshot().Timeline.LatestFrame;
            Assert.Greater(latestFrame, firstFrame);

            Assert.IsTrue(controller.Timeline.TryGetFrame(
                firstFrame,
                out CharacterTestRuntimeAiPlannerDevTimelineRecord firstRecord));
            FieldInfo stateHashField = typeof(CharacterTestRuntimeAiPlannerDevTimelineRecord)
                .GetField("<StateHash>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(stateHashField);
            stateHashField.SetValue(firstRecord, firstRecord.StateHash ^ 1L);
            Assert.IsTrue(controller.BeginTimelineScrub(latestFrame), controller.StatusMessage);
            Assert.IsTrue(controller.UpdateTimelineScrub(firstFrame), controller.StatusMessage);
            Assert.IsTrue(controller.EndTimelineScrub(firstFrame), controller.StatusMessage);
            int restoreBudget = 600;
            while (controller.ReplayInProgress && restoreBudget-- > 0)
                yield return null;

            Assert.Greater(restoreBudget, 0, controller.StatusMessage);
            Assert.IsTrue(controller.SessionContinuable, controller.StatusMessage);
            Assert.AreEqual(firstFrame, manager.RuntimeContext.Slice.CurrentFrame.Value, controller.StatusMessage);
            Assert.AreEqual(firstFrame, controller.CaptureSnapshot().Timeline.SelectedFrame, controller.StatusMessage);
            StringAssert.Contains("已定位并刷新画面", controller.StatusMessage);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_RecomputeFromSelectedFrameTruncatesOnlyAfterFirstNewTick()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            Assert.IsTrue(controller.StepFrames(24), controller.StatusMessage);

            Assert.IsTrue(controller.FreezeReplayValidationCapture(out string oldFreezeDiagnostic),
                oldFreezeDiagnostic);
            CharacterTestRuntimeAiLabReplayValidationWorkspaceSnapshot oldCapture =
                controller.CaptureReplayValidationWorkspaceSnapshot();

            int originalLatest = controller.Timeline.LatestFrame;
            int branchFrame = originalLatest - 12;
            Assert.IsTrue(controller.JumpToFrame(branchFrame), controller.StatusMessage);
            int updateBudget = 120;
            while (controller.ReplayInProgress && updateBudget-- > 0)
                yield return null;
            Assert.Greater(updateBudget, 0, controller.StatusMessage);

            int originalCount = controller.Timeline.Count;
            Assert.IsTrue(controller.ToggleBranchRecompute(), controller.StatusMessage);
            Assert.IsTrue(controller.BranchConfirmationPending);
            Assert.IsFalse(controller.BranchRecomputeArmed);
            Assert.AreEqual(originalLatest, controller.Timeline.LatestFrame);
            Assert.AreEqual(originalCount, controller.Timeline.Count);
            Assert.IsTrue(controller.CancelBranchRecomputeConfirmation());
            Assert.IsFalse(controller.BranchConfirmationPending);
            Assert.IsFalse(controller.BranchRecomputeArmed);
            Assert.AreEqual(originalLatest, controller.Timeline.LatestFrame);
            Assert.AreEqual(originalCount, controller.Timeline.Count);
            Assert.IsTrue(controller.ToggleBranchRecompute(), controller.StatusMessage);
            Assert.IsTrue(controller.BranchConfirmationPending);
            Assert.IsTrue(controller.ToggleBranchRecompute(), controller.StatusMessage);
            Assert.IsTrue(controller.BranchRecomputeArmed);
            Assert.AreEqual(originalLatest, controller.Timeline.LatestFrame);
            Assert.AreEqual(originalCount, controller.Timeline.Count);

            Assert.IsTrue(controller.StepFrame(), controller.StatusMessage);
            Assert.IsFalse(controller.BranchRecomputeArmed);
            Assert.AreEqual(branchFrame + 1, controller.Timeline.LatestFrame);
            Assert.AreEqual(branchFrame + 1, controller.Timeline.SelectedFrame);
            Assert.IsFalse(controller.Timeline.TryGetFrame(branchFrame + 2, out _));

            Assert.AreEqual(CharacterTestReplayValidationState.Empty,
                controller.CaptureReplayValidationWorkspaceSnapshot().State,
                "Committing a branch must invalidate the frozen old-future capture.");
            Assert.IsTrue(controller.StepFrames(6), controller.StatusMessage);
            Assert.IsTrue(controller.FreezeReplayValidationCapture(out string branchFreezeDiagnostic),
                branchFreezeDiagnostic);
            CharacterTestRuntimeAiLabReplayValidationWorkspaceSnapshot branchCapture =
                controller.CaptureReplayValidationWorkspaceSnapshot();
            Assert.Greater(branchCapture.CaptureGeneration, oldCapture.CaptureGeneration);
            Assert.GreaterOrEqual(branchCapture.StartFrame, branchFrame + 1);
            Assert.LessOrEqual(branchCapture.EndFrame, controller.Timeline.LatestFrame);
            Assert.AreNotEqual(oldCapture.TimelineEvidenceHash, branchCapture.TimelineEvidenceHash);
            Assert.IsTrue(controller.StartReplayValidation(out string validationDiagnostic),
                validationDiagnostic);
            updateBudget = 120;
            while (controller.CaptureReplayValidationWorkspaceSnapshot().IsBusy
                && updateBudget-- > 0)
            {
                yield return null;
            }
            Assert.Greater(updateBudget, 0, "Branch replay validation timed out.");
            Assert.AreEqual(
                CharacterTestReplayValidationState.Passed,
                controller.CaptureReplayValidationWorkspaceSnapshot().State,
                controller.CaptureReplayValidationWorkspaceSnapshot().Summary);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_ArmedHistoricalScrubRebuildsContiguousFuture()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            Assert.IsTrue(controller.StepFrames(24), controller.StatusMessage);

            int originalLatest = controller.Timeline.LatestFrame;
            int branchFrame = originalLatest - 12;
            int targetFrame = branchFrame + 6;
            Assert.IsTrue(controller.JumpToFrame(branchFrame), controller.StatusMessage);
            int updateBudget = 120;
            while (controller.ReplayInProgress && updateBudget-- > 0)
                yield return null;
            Assert.Greater(updateBudget, 0, controller.StatusMessage);

            Assert.IsTrue(controller.ToggleBranchRecompute(), controller.StatusMessage);
            Assert.IsTrue(controller.BranchConfirmationPending);
            Assert.IsTrue(controller.ToggleBranchRecompute(), controller.StatusMessage);
            Assert.IsTrue(controller.BranchRecomputeArmed);
            Assert.IsTrue(controller.BeginTimelineScrub(targetFrame), controller.StatusMessage);
            updateBudget = 120;
            while (controller.Timeline.LatestFrame < targetFrame && updateBudget-- > 0)
                yield return null;
            Assert.Greater(updateBudget, 0, controller.StatusMessage);
            Assert.IsTrue(controller.EndTimelineScrub(targetFrame), controller.StatusMessage);

            Assert.AreEqual(targetFrame, controller.Timeline.LatestFrame);
            Assert.AreEqual(targetFrame, controller.Timeline.SelectedFrame);
            Assert.IsFalse(controller.Timeline.TryGetFrame(targetFrame + 1, out _),
                "The old future must be removed after the first successful branch tick.");
            for (int frame = branchFrame; frame <= targetFrame; frame++)
                Assert.IsTrue(controller.Timeline.TryGetFrame(frame, out _), $"Missing rebuilt frame {frame}.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_HistoricalPlayStartsAtSelectionWithoutJumpingToLatest()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            Assert.IsTrue(controller.StepFrames(16), controller.StatusMessage);
            int originalLatest = controller.Timeline.LatestFrame;
            int selected = originalLatest - 8;
            Assert.IsTrue(controller.JumpToFrame(selected), controller.StatusMessage);
            int updateBudget = 120;
            while (controller.ReplayInProgress && updateBudget-- > 0)
                yield return null;

            Assert.IsTrue(controller.TogglePause(), controller.StatusMessage);
            Assert.IsTrue(controller.HistoryPlaybackActive);
            Assert.AreEqual(selected, controller.Timeline.SelectedFrame);
            Assert.AreEqual(originalLatest, controller.Timeline.LatestFrame);
            Assert.IsTrue(controller.TogglePause(), controller.StatusMessage);
            Assert.IsFalse(controller.HistoryPlaybackActive);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_UnarmedHistoricalScrubCannotGenerateFuture()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            Assert.IsTrue(controller.StepFrames(20), controller.StatusMessage);
            int originalLatest = controller.Timeline.LatestFrame;
            Assert.IsTrue(controller.JumpToFrame(originalLatest - 10), controller.StatusMessage);
            int updateBudget = 120;
            while (controller.ReplayInProgress && updateBudget-- > 0)
                yield return null;

            int requestedFuture = originalLatest + 10;
            Assert.IsTrue(controller.BeginTimelineScrub(requestedFuture), controller.StatusMessage);
            Assert.IsTrue(controller.EndTimelineScrub(requestedFuture), controller.StatusMessage);
            updateBudget = 120;
            while (controller.ReplayInProgress && updateBudget-- > 0)
                yield return null;

            Assert.AreEqual(originalLatest, controller.Timeline.LatestFrame);
            Assert.AreEqual(originalLatest, controller.Timeline.SelectedFrame);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_FutureScrubClampsAtFrameLimit()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            Assert.IsTrue(controller.TrySetFrameLimit(20));
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            int sessionEndFrame = controller.Timeline.Records[0].Frame + controller.FrameLimit - 1;
            Assert.IsTrue(controller.BeginTimelineScrub(sessionEndFrame + 100), controller.StatusMessage);
            Assert.IsTrue(controller.EndTimelineScrub(sessionEndFrame + 100), controller.StatusMessage);
            int updateBudget = 120;
            while ((controller.ReplayInProgress || controller.FutureAdvanceActive)
                && updateBudget-- > 0)
                yield return null;
            Assert.Greater(updateBudget, 0, controller.StatusMessage);
            Assert.AreEqual(controller.FrameLimit, controller.Timeline.Count);
            Assert.AreEqual(sessionEndFrame, controller.Timeline.LatestFrame);
            Assert.AreEqual(sessionEndFrame, controller.Timeline.SelectedFrame);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerDevScenePhase.Completed, controller.Phase);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_ScrubCommitsRetainedFrameAndKeepsPaused()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(manager);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            Assert.IsTrue(controller.StepFrames(40), controller.StatusMessage);

            int targetFrame = controller.Timeline.LatestFrame - 12;
            Assert.IsTrue(controller.BeginTimelineScrub(targetFrame), controller.StatusMessage);
            Assert.IsTrue(controller.EndTimelineScrub(targetFrame), controller.StatusMessage);
            int updateBudget = 60;
            while (controller.ReplayInProgress && updateBudget-- > 0)
                yield return null;

            Assert.IsFalse(controller.ReplayInProgress, controller.StatusMessage);
            Assert.AreEqual(targetFrame, controller.Timeline.SelectedFrame, controller.StatusMessage);
            Assert.AreEqual(targetFrame, manager.RuntimeContext.Slice.CurrentFrame.Value, controller.StatusMessage);
            Assert.IsTrue(manager.RuntimeContext.Paused);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_LongSeekUsesBudgetedDataReplay()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(manager);
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            GameSlice warmedSlice = manager.RuntimeContext.Slice;
            Assert.IsTrue(controller.StepFrames(130), controller.StatusMessage);

            int targetFrame = controller.Timeline.LatestFrame;
            CharacterTestRuntimeAiLabFrameSyncSample expected =
                CaptureSceneControllerFrameSyncSample(controller, manager);
            Assert.IsTrue(controller.Timeline.TryGetFrame(
                targetFrame,
                out CharacterTestRuntimeAiPlannerDevTimelineRecord targetRecord));
            Assert.IsTrue(controller.StepFrames(10), controller.StatusMessage);
            Assert.IsTrue(controller.StepBack10(), controller.StatusMessage);
            Assert.IsTrue(controller.ReplayInProgress);
            Assert.Greater(controller.ReplayTotalFrames, 120);

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel replayViewModel =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                    controller,
                    commandStatus: null);
            Assert.IsFalse(replayViewModel.CanStep);
            Assert.IsFalse(replayViewModel.CanStepBack);
            Assert.IsFalse(replayViewModel.CanJumpFrame);
            StringAssert.Contains("Warm Replay", controller.StatusMessage);

            int updateBudget = 600;
            while (controller.ReplayInProgress && updateBudget-- > 0)
                yield return null;

            Assert.IsFalse(controller.ReplayInProgress, controller.StatusMessage);
            Assert.Greater(updateBudget, 0, "Budgeted replay did not finish within 600 Updates.");
            Assert.AreEqual(
                targetFrame,
                manager.RuntimeContext.Slice.CurrentFrame.Value,
                controller.StatusMessage);
            Assert.AreSame(
                warmedSlice,
                manager.RuntimeContext.Slice,
                "Warm Replay must retain the warmed GameSlice instead of rebuilding the session.");
            Assert.AreEqual(targetFrame, controller.Timeline.SelectedFrame);
            CharacterTestRuntimeAiLabAuthoritySample replayed =
                CaptureSceneControllerAuthoritySample(manager);
            Assert.AreEqual(targetRecord.StateHash, replayed.StateHash);
            CharacterTestRuntimeAiLabFrameSyncSample actual =
                CaptureSceneControllerFrameSyncSample(controller, manager);
            CharacterTestRuntimeAiLabFrameSyncReport presentationReport =
                CharacterTestRuntimeAiLabFrameSyncProbe.Compare(expected, actual);
            for (int mismatchIndex = 0;
                 mismatchIndex < presentationReport.MismatchRows.Count;
                 mismatchIndex++)
            {
                CharacterTestRuntimeAiLabFrameSyncMismatch mismatch =
                    presentationReport.MismatchRows[mismatchIndex];
                bool animationOrVisibleFrameMismatch = mismatch.Layer == "animation"
                    || mismatch.Layer == "animationInput"
                    || mismatch.Layer == "bonePose"
                    || mismatch.Layer == "procedural"
                    || mismatch.Layer == "view"
                    || mismatch.Layer == "overlay";
                Assert.IsFalse(
                    animationOrVisibleFrameMismatch,
                    presentationReport.SummaryText);
            }
            StringAssert.Contains("Warm Replay PASS", controller.StatusMessage);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_ColdBootstrapVerifyRebuildsOnlyWhenExplicit()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(manager);
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            Assert.IsTrue(controller.StepForward10(), controller.StatusMessage);
            GameSlice warmedSlice = manager.RuntimeContext.Slice;
            int targetFrame = controller.Timeline.SelectedFrame;
            Assert.IsTrue(controller.Timeline.TryGetFrame(
                targetFrame,
                out CharacterTestRuntimeAiPlannerDevTimelineRecord target));

            Assert.IsTrue(controller.ColdVerifySelectedFrame(), controller.StatusMessage);
            int updateBudget = 120;
            while (controller.ReplayInProgress && updateBudget-- > 0)
                yield return null;

            Assert.IsFalse(controller.ReplayInProgress, controller.StatusMessage);
            Assert.AreNotSame(warmedSlice, manager.RuntimeContext.Slice);
            Assert.AreEqual(targetFrame, manager.RuntimeContext.Slice.CurrentFrame.Value);
            CharacterTestRuntimeAiLabAuthoritySample actual =
                CaptureSceneControllerAuthoritySample(manager);
            Assert.AreEqual(target.StateHash, actual.StateHash);
            StringAssert.Contains("Cold Bootstrap Verify PASS", controller.StatusMessage);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_LoopReviewReplaysRecordedRangeWithoutMutation()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(manager);
            controller.PrepareInitialSetupState(restorePersistedDraft: false);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);
            yield return null;

            CharacterTestRuntimeAiLabFrameSyncSample recordedA =
                CaptureSceneControllerFrameSyncSample(controller, manager);
            CharacterTestRuntimeAiLabAuthoritySample recordedAAuthority =
                CaptureSceneControllerAuthoritySample(manager);
            Assert.IsTrue(controller.SetLoopStart(), controller.StatusMessage);
            Assert.IsTrue(controller.StepForward10(), controller.StatusMessage);
            CharacterTestRuntimeAiLabFrameSyncSample recordedB =
                CaptureSceneControllerFrameSyncSample(controller, manager);
            CharacterTestRuntimeAiLabAuthoritySample recordedBAuthority =
                CaptureSceneControllerAuthoritySample(manager);
            Assert.IsTrue(controller.SetLoopEnd(), controller.StatusMessage);

            int originalCount = controller.Timeline.Count;
            int originalLatest = controller.Timeline.LatestFrame;
            long[] originalHashes = new long[originalCount];
            for (int i = 0; i < originalCount; i++)
                originalHashes[i] = controller.Timeline.Records[i].StateHash;

            Assert.IsTrue(controller.ToggleLoop(), controller.StatusMessage);
            Assert.IsTrue(controller.TogglePause(), controller.StatusMessage);
            Assert.IsTrue(controller.LoopPlaybackActive);
            Assert.IsTrue(manager.RuntimeContext.Paused);
            Assert.AreEqual(recordedA.RuntimeFrame, controller.Timeline.SelectedFrame);

            for (int i = recordedA.RuntimeFrame; i < recordedB.RuntimeFrame; i++)
                Assert.IsTrue(controller.AdvanceLoopReviewFrame(), controller.StatusMessage);
            CharacterTestRuntimeAiLabFrameSyncSample firstLoopB =
                CaptureSceneControllerFrameSyncSample(controller, manager);
            CharacterTestRuntimeAiLabAuthoritySample firstLoopBAuthority =
                CaptureSceneControllerAuthoritySample(manager);

            Assert.IsTrue(controller.AdvanceLoopReviewFrame(), controller.StatusMessage);
            CharacterTestRuntimeAiLabFrameSyncSample loopA =
                CaptureSceneControllerFrameSyncSample(controller, manager);
            CharacterTestRuntimeAiLabAuthoritySample loopAAuthority =
                CaptureSceneControllerAuthoritySample(manager);

            for (int i = recordedA.RuntimeFrame; i < recordedB.RuntimeFrame; i++)
                Assert.IsTrue(controller.AdvanceLoopReviewFrame(), controller.StatusMessage);
            CharacterTestRuntimeAiLabFrameSyncSample secondLoopB =
                CaptureSceneControllerFrameSyncSample(controller, manager);
            CharacterTestRuntimeAiLabAuthoritySample secondLoopBAuthority =
                CaptureSceneControllerAuthoritySample(manager);

            CharacterTestRuntimeAiLabFrameSyncReport reportA =
                CharacterTestRuntimeAiLabAuthorityProbe.Compare(recordedAAuthority, loopAAuthority);
            CharacterTestRuntimeAiLabFrameSyncReport reportFirstB =
                CharacterTestRuntimeAiLabAuthorityProbe.Compare(
                    recordedBAuthority,
                    firstLoopBAuthority);
            CharacterTestRuntimeAiLabFrameSyncReport reportSecondB =
                CharacterTestRuntimeAiLabAuthorityProbe.Compare(
                    recordedBAuthority,
                    secondLoopBAuthority);

            Assert.AreEqual(originalCount, controller.Timeline.Count);
            Assert.AreEqual(originalLatest, controller.Timeline.LatestFrame);
            for (int i = 0; i < originalCount; i++)
                Assert.AreEqual(originalHashes[i], controller.Timeline.Records[i].StateHash, "frame index " + i);
            Assert.AreEqual(recordedB.RuntimeFrame, secondLoopB.RuntimeFrame);

            Assert.IsTrue(controller.TogglePause(), controller.StatusMessage);
            Assert.IsFalse(controller.LoopPlaybackActive);
            Assert.IsTrue(controller.ToggleLoop(), controller.StatusMessage);
            Assert.IsFalse(controller.Timeline.LoopEnabled);

            yield return new ExitPlayMode();

            Assert.IsTrue(reportA.Passed, reportA.SummaryText);
            Assert.IsTrue(reportFirstB.Passed, reportFirstB.SummaryText);
            Assert.IsTrue(reportSecondB.Passed, reportSecondB.SummaryText);
        }

        private static bool HasAdvancedAnimationClock(
            CharacterTestAnimationPresentationCheckpoint before,
            CharacterTestAnimationPresentationCheckpoint after)
        {
            for (int beforeIndex = 0; beforeIndex < before.Characters.Count; beforeIndex++)
            {
                CharacterTestRuntimeCharacterPresentationState beforeCharacter =
                    before.Characters[beforeIndex];
                if (beforeCharacter.Animation == null)
                    continue;

                for (int afterIndex = 0; afterIndex < after.Characters.Count; afterIndex++)
                {
                    CharacterTestRuntimeCharacterPresentationState afterCharacter =
                        after.Characters[afterIndex];
                    if (afterCharacter.EntityId != beforeCharacter.EntityId
                        || afterCharacter.Animation == null)
                    {
                        continue;
                    }

                    if (afterCharacter.Animation.RuntimeAnimationClock
                        > beforeCharacter.Animation.RuntimeAnimationClock)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
