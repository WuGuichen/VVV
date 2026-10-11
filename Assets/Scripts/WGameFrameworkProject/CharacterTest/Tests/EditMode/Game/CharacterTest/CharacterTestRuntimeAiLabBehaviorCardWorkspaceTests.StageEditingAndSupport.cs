using System;
using System.Reflection;
using MxFramework.UI;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceTests
    {

        [Test]
        public void NativeTimeline_AddCreatesExplicitReachableJumpAndOneUndo()
        {

            using var f = new NativeEditingFixture();
            int count = f.Payload().Timelines.Count;
            Assert.IsTrue(f.Controller.TryAddLogicStage(NativeEditingFixture.Entry, 0, out string issue), issue);
            var payload = f.Payload();
            Assert.AreEqual(count + 1, payload.Timelines.Count);
            var target = payload.Timelines[1];
            var jump = payload.Timelines[0].Events.Find(e => e.Operation.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline);
            Assert.NotNull(jump);
            Assert.AreEqual(target.TimelineId, jump.Operation.TargetTimelineId);
            Assert.AreEqual(30, jump.StartFrame);
            var compiled = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.IsTrue(compiled.Success, JoinDiagnostics(compiled));
            f.Undo();
            Assert.AreEqual(count, f.Payload().Timelines.Count);
        }

        private static bool ContainsDiagnostic(
            System.Collections.Generic.IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code)
        {
            for (int i = 0; diagnostics != null && i < diagnostics.Count; i++)
            {
                if (string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string DescribeRecipeEvents(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline)
        {
            if (timeline == null || timeline.TriggerEvents.Count == 0)
                return "<none>";
            var descriptions = new string[timeline.TriggerEvents.Count];
            for (int i = 0; i < timeline.TriggerEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent recipeEvent =
                    timeline.TriggerEvents[i];
                descriptions[i] = recipeEvent.EventId + ":" + recipeEvent.SourceKind
                    + "/" + recipeEvent.LocalFrameAnchor + "->"
                    + recipeEvent.OutcomeKind + "/" + recipeEvent.TargetTimelineId;
            }
            return string.Join(", ", descriptions);
        }

        [Test]
        public void NativeEventTargetAndExitChangesRejectStaleIdentity()
        {

            using var f = new NativeEditingFixture();
            Assert.IsTrue(f.Controller.TryAddLogicStage(NativeEditingFixture.Entry, 0, out string issue), issue);
            string target = f.Payload().Timelines[1].TimelineId;
            f.Edit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationKind,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline));
            f.Edit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.TargetTimelineId,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(stringValue: target));
            Assert.AreEqual(target, f.Event().Operation.TargetTimelineId);
            var stale = f.Identity();
            f.Edit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationKind,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner));
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner, f.Event().Operation.Kind);
            Assert.IsFalse(f.Controller.TryApplyBehaviorRecipeTriggerEventField(NativeEditingFixture.Entry, stale,
                "approach", "event.exit", CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationKind, -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline), out _, out _));
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner, f.Event().Operation.Kind);
        }

        [Test]
        public void NativeEventLabelEditPreservesOutcomeAndConditionIdentity()
        {

            using var f = new NativeEditingFixture();
            string eventId = f.Event().EventId, condition = f.Event().ConditionRoot.NodeId;
            int revision = f.Controller.DraftWorkspace.CurrentRevision;
            f.Edit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(stringValue: "Return after target loss"));
            Assert.AreEqual(eventId, f.Event().EventId);
            Assert.AreEqual(condition, f.Event().ConditionRoot.NodeId);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner, f.Event().Operation.Kind);
            Assert.AreEqual("Return after target loss", f.Event().Title);
            Assert.AreEqual(revision + 1, f.Controller.DraftWorkspace.CurrentRevision);
            f.Undo();
            Assert.AreEqual(eventId, f.Event().EventId);
            Assert.AreEqual(condition, f.Event().ConditionRoot.NodeId);
        }

        [Test]
        public void StageMarquee_UsesStableIdsAndRejectsStaleRevision()
        {
            var host = new GameObject("Behavior Card Stage Marquee Identity Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.That(controller.TryCreateReactiveChaseDodgeGroup(0, out string presetMessage),
                    Is.True, presetMessage);
                if (controller.AuthoringMode != CharacterTestRuntimeAiLabAuthoringMode.Developer)
                    controller.ToggleAuthoringMode();

                const int entryIndex = 2;
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace =
                    ProjectDraft(controller, entryIndex);
                var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                handler.Configure(
                    controller,
                    null,
                    null,
                    () => workspace = ProjectDraft(controller, entryIndex),
                    () => entryIndex,
                    workspaceView: () => workspace);
                string stageId = workspace.Timeline.Stages[0].StageId;
                Assert.That(handler.SelectStage(
                    stageId,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None), Is.True);

                Assert.That(handler.BeginStageMarquee(out long validEpoch), Is.True);
                Array.Reverse(workspace.Timeline.Stages, 0, workspace.Timeline.StageCount);
                Assert.That(handler.CommitStageMarquee(
                    validEpoch,
                    new[] { stageId },
                    1,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None,
                    0), Is.True);
                Assert.That(handler.PrimaryStageId, Is.EqualTo(stageId));

                Assert.That(handler.BeginStageMarquee(out long staleEpoch), Is.True);
                Assert.That(handler.NudgeSelectedStages(1), Is.True);
                Assert.That(handler.CommitStageMarquee(
                    staleEpoch,
                    new[] { stageId },
                    1,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None,
                    0), Is.False);
                Assert.That(handler.PrimaryStageId, Is.EqualTo(stageId));
                Assert.That(handler.HasActiveStageMarquee, Is.False);
                StringAssert.Contains("过期", handler.LastMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void UndoStageEdit_FirstCancelsActiveGestureThenSecondUndoRestoresDraft()
        {
            var host = new GameObject("Behavior Card Stage Undo Gesture Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.That(controller.TryCreateReactiveChaseDodgeGroup(0, out string presetMessage),
                    Is.True, presetMessage);
                if (controller.AuthoringMode != CharacterTestRuntimeAiLabAuthoringMode.Developer)
                    controller.ToggleAuthoringMode();

                const int entryIndex = 2;
                NormalizeNativeDrafts(controller);
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace =
                    ProjectDraft(controller, entryIndex);
                var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                handler.Configure(
                    controller,
                    null,
                    null,
                    () => workspace = ProjectDraft(controller, entryIndex),
                    () => entryIndex,
                    workspaceView: () => workspace);
                string stageId = workspace.Timeline.Stages[0].StageId;
                CharacterTestRuntimeAiLabLogicStage originalStage =
                    CurrentDraftPayload(controller, entryIndex).Stages.Find(
                        candidate => string.Equals(
                            candidate?.StageId,
                            stageId,
                            StringComparison.Ordinal));
                Assert.That(originalStage, Is.Not.Null);
                string originalTitle = originalStage.Title;
                const string editedTitle = "Edited Timeline";
                Assert.That(handler.SelectStage(
                    stageId,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None), Is.True);
                Assert.That(handler.BeginStageFieldEdit(
                    stageId,
                    CharacterTestRuntimeAiLabStageFieldKind.Title,
                    out long fieldEpoch), Is.True);
                Assert.That(handler.CommitStageFieldEdit(
                    fieldEpoch,
                    stageId,
                    CharacterTestRuntimeAiLabStageFieldKind.Title,
                    new CharacterTestRuntimeAiLabStageFieldValue(stringValue: editedTitle)), Is.True);
                Assert.That(
                    CurrentDraftPayload(controller, entryIndex).Stages.Find(
                        candidate => string.Equals(
                            candidate?.StageId,
                            stageId,
                            StringComparison.Ordinal)).Title,
                    Is.EqualTo(editedTitle));

                Assert.That(handler.BeginStageGesture(
                    CharacterTestRuntimeAiLabStageGestureKind.Move,
                    stageId,
                    100f,
                    2f,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None,
                    out _), Is.True);
                handler.UndoStageEdit();
                Assert.That(handler.HasActiveStageGesture, Is.False);
                Assert.That(
                    CurrentDraftPayload(controller, entryIndex).Stages.Find(
                        candidate => string.Equals(
                            candidate?.StageId,
                            stageId,
                            StringComparison.Ordinal)).Title,
                    Is.EqualTo(editedTitle));
                StringAssert.Contains("再次执行", handler.LastMessage);

                handler.UndoStageEdit();
                Assert.That(
                    CurrentDraftPayload(controller, entryIndex).Stages.Find(
                        candidate => string.Equals(
                            candidate?.StageId,
                            stageId,
                            StringComparison.Ordinal)).Title,
                    Is.EqualTo(originalTitle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RootUndoCommand_UsesBehaviorCardActiveSessionGuard()
        {
            var host = new GameObject("Behavior Card Root Undo Guard Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.That(controller.TryCreateReactiveChaseDodgeGroup(0, out string presetMessage),
                    Is.True, presetMessage);
                if (controller.AuthoringMode != CharacterTestRuntimeAiLabAuthoringMode.Developer)
                    controller.ToggleAuthoringMode();
                const int entryIndex = 2;
                EnterBehaviorCardWorkspace(controller, entryIndex);
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace =
                    ProjectDraft(controller, entryIndex);
                var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                handler.Configure(
                    controller,
                    null,
                    null,
                    () => workspace = ProjectDraft(controller, entryIndex),
                    () => entryIndex,
                    workspaceView: () => workspace);
                var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(controller);
                sink.ConfigureBehaviorCardWorkspaceCommands(handler);

                string stageId = workspace.Timeline.Stages[0].StageId;
                Assert.That(handler.SelectStage(
                    stageId,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None), Is.True);
                Assert.That(handler.NudgeSelectedStages(15), Is.True);
                int movedStart = CurrentDraftPayload(controller, entryIndex).Stages[0].PlannedStartFrame;
                Assert.That(handler.BeginStageGesture(
                    CharacterTestRuntimeAiLabStageGestureKind.Move,
                    stageId,
                    100f,
                    2f,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None,
                    out _), Is.True);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.UndoDraft,
                    null));

                Assert.That(handler.HasActiveStageGesture, Is.False);
                Assert.That(
                    CurrentDraftPayload(controller, entryIndex).Stages[0].PlannedStartFrame,
                    Is.EqualTo(movedStart));
                StringAssert.Contains("再次执行", sink.LastStatusMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ReconcileStageEditing_SwitchingCardsDoesNotReusePreviousHistoryRoute()
        {
            var host = new GameObject("Behavior Card Stage Route Switch Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.That(controller.TryCreateReactiveChaseDodgeGroup(0, out string presetMessage),
                    Is.True, presetMessage);
                if (controller.AuthoringMode != CharacterTestRuntimeAiLabAuthoringMode.Developer)
                    controller.ToggleAuthoringMode();

                const int firstEntryIndex = 1;
                const int secondEntryIndex = 2;
                Assert.That(controller.TryResolveStageEditIdentity(
                    firstEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity firstIdentity,
                    out string firstIdentityMessage), Is.True, firstIdentityMessage);
                Assert.That(controller.TryResolveStageEditIdentity(
                    secondEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity secondIdentity,
                    out string secondIdentityMessage), Is.True, secondIdentityMessage);

                CharacterTestRuntimeAiLabShellCoordinator coordinator =
                    controller.EnsureShellCoordinator(playerDeckAvailable: true);
                Assert.That(coordinator.AttachMountAdapter(
                    coordinator.CurrentState.Generation,
                    new BehaviorCardMountAdapter()).Success, Is.True);
                CharacterTestRuntimeAiLabShellState shell = coordinator.CurrentState;
                Assert.That(coordinator.NavigateToBehaviorCard(
                    shell.Generation,
                    shell.Revision,
                    shell.ActiveWorkspace,
                    firstIdentity.GroupDraftId,
                    firstIdentity.EntryId,
                    firstIdentity.CardDraftId,
                    CharacterTestRuntimeAiLabSelectedElementKind.None,
                    string.Empty,
                    CharacterTestRuntimeAiLabBehaviorCardViewport.Default).Success, Is.True);

                int selectedEntryIndex = firstEntryIndex;
                NormalizeNativeDrafts(controller);
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace =
                    ProjectDraft(controller, selectedEntryIndex);
                var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                handler.Configure(
                    controller,
                    null,
                    null,
                    () => workspace = ProjectDraft(controller, selectedEntryIndex),
                    () => selectedEntryIndex,
                    workspaceView: () => workspace);
                string firstStageId = workspace.Timeline.Stages[0].StageId;
                Assert.That(handler.SelectStage(
                    firstStageId,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None), Is.True);

                shell = coordinator.CurrentState;
                Assert.That(coordinator.Navigate(
                    shell.Generation,
                    shell.Revision,
                    shell.ActiveWorkspace,
                    CharacterTestRuntimeAiLabWorkspace.BehaviorGroup).Success, Is.True);
                selectedEntryIndex = secondEntryIndex;
                workspace = ProjectDraft(controller, selectedEntryIndex);
                shell = coordinator.CurrentState;
                Assert.That(coordinator.NavigateToBehaviorCard(
                    shell.Generation,
                    shell.Revision,
                    shell.ActiveWorkspace,
                    secondIdentity.GroupDraftId,
                    secondIdentity.EntryId,
                    secondIdentity.CardDraftId,
                    CharacterTestRuntimeAiLabSelectedElementKind.None,
                    string.Empty,
                    CharacterTestRuntimeAiLabBehaviorCardViewport.Default).Success, Is.True);

                string firstTitle = CurrentDraftPayload(controller, firstEntryIndex).Stages[0].Title;
                string secondTitle = CurrentDraftPayload(controller, secondEntryIndex).Stages[0].Title;
                string editedSecondTitle = secondTitle + " edited";
                string secondStageId = workspace.Timeline.Stages[0].StageId;
                Assert.That(handler.SelectStage(
                    secondStageId,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None), Is.True);
                Assert.That(handler.BeginStageFieldEdit(
                    secondStageId,
                    CharacterTestRuntimeAiLabStageFieldKind.Title,
                    out long fieldEpoch), Is.True, handler.LastMessage);
                Assert.That(handler.CommitStageFieldEdit(
                    fieldEpoch,
                    secondStageId,
                    CharacterTestRuntimeAiLabStageFieldKind.Title,
                    new CharacterTestRuntimeAiLabStageFieldValue(
                        stringValue: editedSecondTitle)), Is.True, handler.LastMessage);
                Assert.That(
                    CurrentDraftPayload(controller, firstEntryIndex).Stages[0].Title,
                    Is.EqualTo(firstTitle));
                Assert.That(
                    CurrentDraftPayload(controller, secondEntryIndex).Stages[0].Title,
                    Is.EqualTo(editedSecondTitle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ReconcileStageEditing_ReorderedDuplicateUsesPageEntryRouteBeforeSharedCardId()
        {
            var host = new GameObject("Behavior Card Shared Draft Route Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.That(controller.TryCreateReactiveChaseDodgeGroup(0, out string presetMessage),
                    Is.True, presetMessage);
                if (controller.AuthoringMode != CharacterTestRuntimeAiLabAuthoringMode.Developer)
                    controller.ToggleAuthoringMode();
                string[] entryIdsBeforeFirstDuplicate = CurrentEntryIds(controller);
                Assert.That(controller.DraftWorkspace.TryDuplicateEntry(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    2,
                    out string duplicateMessage), Is.True, duplicateMessage);
                int firstDuplicateIndex = FindNewEntryIndex(
                    controller,
                    entryIdsBeforeFirstDuplicate);
                string firstDuplicateEntryId = CurrentEntryIds(controller)[firstDuplicateIndex];
                string[] entryIdsBeforeSecondDuplicate = CurrentEntryIds(controller);
                Assert.That(controller.DraftWorkspace.TryDuplicateEntry(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    2,
                    out string secondDuplicateMessage), Is.True, secondDuplicateMessage);
                int secondDuplicateIndex = FindNewEntryIndex(
                    controller,
                    entryIdsBeforeSecondDuplicate);
                string secondDuplicateEntryId = CurrentEntryIds(controller)[secondDuplicateIndex];
                firstDuplicateIndex = FindEntryIndex(controller, firstDuplicateEntryId);
                secondDuplicateIndex = FindEntryIndex(controller, secondDuplicateEntryId);

                int selectedEntryIndex = secondDuplicateIndex;
                int moveDirection = selectedEntryIndex > firstDuplicateIndex ? -1 : 1;
                int movedEntryIndex = selectedEntryIndex + moveDirection;
                int siblingEntryIndex = selectedEntryIndex;
                Assert.That(controller.TryResolveStageEditIdentity(
                    selectedEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity duplicateIdentity,
                    out string identityMessage), Is.True, identityMessage);
                EnterBehaviorCardWorkspace(controller, selectedEntryIndex);
                NormalizeNativeDrafts(controller);
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace =
                    ProjectDraft(controller, selectedEntryIndex);
                var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                handler.Configure(
                    controller,
                    null,
                    null,
                    null,
                    () => selectedEntryIndex,
                    workspaceView: () => workspace);
                string stageId = workspace.Timeline.Stages[0].StageId;
                Assert.That(handler.SelectStage(
                    stageId,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None), Is.True);

                Assert.That(controller.DraftWorkspace.TryMoveEntry(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    selectedEntryIndex,
                    moveDirection,
                    out string moveMessage), Is.True, moveMessage);
                Assert.That(controller.TryResolveStageEditIdentity(
                    movedEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity movedDuplicateIdentity,
                    out identityMessage), Is.True, identityMessage);
                Assert.That(
                    movedDuplicateIdentity.EntryId,
                    Is.EqualTo(duplicateIdentity.EntryId),
                    "The moved array slot must still resolve to the selected duplicate EntryId.");
                Assert.That(controller.TryResolveStageEditIdentity(
                    siblingEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity staleIndexIdentity,
                    out identityMessage), Is.True, identityMessage);
                Assert.That(
                    staleIndexIdentity.EntryId,
                    Is.Not.EqualTo(duplicateIdentity.EntryId),
                    "The stale index must now refer to the sibling duplicate.");
                Assert.That(
                    staleIndexIdentity.CardDraftId,
                    Is.EqualTo(duplicateIdentity.CardDraftId),
                    "Both duplicates must still share the source card before copy-on-write.");
                string duplicateTitle = CurrentDraftPayload(controller, movedEntryIndex).Stages[0].Title;
                Assert.AreSame(CurrentDraftPayload(controller, movedEntryIndex), CurrentDraftPayload(controller, siblingEntryIndex), "Duplicated entries share their card definition; only entry routes are distinct.");
                string editedDuplicateTitle = duplicateTitle + " routed";

                Assert.That(handler.BeginStageFieldEdit(
                    stageId,
                    CharacterTestRuntimeAiLabStageFieldKind.Title,
                    out long fieldEpoch), Is.True, handler.LastMessage);
                Assert.That(handler.CommitStageFieldEdit(
                    fieldEpoch,
                    stageId,
                    CharacterTestRuntimeAiLabStageFieldKind.Title,
                    new CharacterTestRuntimeAiLabStageFieldValue(
                        stringValue: editedDuplicateTitle)), Is.True, handler.LastMessage);
                Assert.That(
                    CurrentDraftPayload(controller, movedEntryIndex).Stages[0].Title,
                    Is.EqualTo(editedDuplicateTitle));
                Assert.That(
                    CurrentDraftPayload(controller, siblingEntryIndex).Stages[0].Title,
                    Is.EqualTo(editedDuplicateTitle));
                Assert.That(controller.TryResolveStageEditIdentity(
                    movedEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity editedDuplicateIdentity,
                    out identityMessage), Is.True, identityMessage);
                Assert.That(
                    editedDuplicateIdentity.EntryId,
                    Is.EqualTo(duplicateIdentity.EntryId),
                    "The field edit must remain routed by EntryId after copy-on-write.");
                Assert.That(
                    editedDuplicateIdentity.CardDraftId,
                    Is.EqualTo(staleIndexIdentity.CardDraftId),
                    "Duplicated entries share the same card definition; editing preserves that identity while the selected entry route remains stable.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void StageHistory_ResolvesStableEntryRouteWhenIndexMoved()
        {
            var host = new GameObject("Behavior Card Stable History Route Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.That(controller.TryCreateReactiveChaseDodgeGroup(0, out string presetMessage),
                    Is.True, presetMessage);
                string[] entryIdsBeforeFirstDuplicate = CurrentEntryIds(controller);
                Assert.That(controller.DraftWorkspace.TryDuplicateEntry(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    2,
                    out string duplicateMessage), Is.True, duplicateMessage);
                int firstDuplicateIndex = FindNewEntryIndex(
                    controller,
                    entryIdsBeforeFirstDuplicate);
                string firstDuplicateEntryId = CurrentEntryIds(controller)[firstDuplicateIndex];
                string[] entryIdsBeforeSecondDuplicate = CurrentEntryIds(controller);
                Assert.That(controller.DraftWorkspace.TryDuplicateEntry(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    2,
                    out string secondDuplicateMessage), Is.True, secondDuplicateMessage);
                int originalIndex = FindNewEntryIndex(
                    controller,
                    entryIdsBeforeSecondDuplicate);
                firstDuplicateIndex = FindEntryIndex(controller, firstDuplicateEntryId);
                int moveDirection = originalIndex > firstDuplicateIndex ? -1 : 1;
                int movedExpectedIndex = originalIndex + moveDirection;
                Assert.That(controller.TryResolveStageEditIdentity(
                    originalIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity originalIdentity,
                    out string identityMessage), Is.True, identityMessage);
                Assert.That(controller.DraftWorkspace.TryMoveEntry(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    originalIndex,
                    moveDirection,
                    out string moveMessage), Is.True, moveMessage);
                Assert.That(controller.TryResolveStageEditIdentityByRoute(
                    originalIdentity,
                    out int movedIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity movedIdentity,
                    out string movedIdentityMessage), Is.True, movedIdentityMessage);
                Assert.That(
                    movedIndex,
                    Is.EqualTo(movedExpectedIndex),
                    "The route resolver must locate the EntryId after the move.");

                Assert.That(controller.TryUndoBehaviorCardEdit(
                    originalIndex,
                    movedIdentity,
                    out int undoIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity undoIdentity,
                    out string undoMessage), Is.True, undoMessage);
                Assert.That(
                    undoIndex,
                    Is.EqualTo(originalIndex),
                    "Undo must restore the EntryId to its pre-move index.");
                Assert.That(undoIdentity.EntryId, Is.EqualTo(originalIdentity.EntryId));

                Assert.That(controller.TryRedoBehaviorCardEdit(
                    originalIndex,
                    undoIdentity,
                    out int redoIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity redoIdentity,
                    out string redoMessage), Is.True, redoMessage);
                Assert.That(
                    redoIndex,
                    Is.EqualTo(movedExpectedIndex),
                    "Redo must restore the EntryId to its moved index.");
                Assert.That(redoIdentity.EntryId, Is.EqualTo(originalIdentity.EntryId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void StageHistory_RejectsStaleRevisionForSameCardRoute()
        {
            var host = new GameObject("Behavior Card Stale History Revision Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.That(controller.TryCreateReactiveChaseDodgeGroup(0, out string presetMessage),
                    Is.True, presetMessage);
                if (controller.AuthoringMode != CharacterTestRuntimeAiLabAuthoringMode.Developer)
                    controller.ToggleAuthoringMode();
                const int entryIndex = 2;
                Assert.That(controller.TryResolveStageEditIdentity(
                    entryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity firstIdentity,
                    out string identityMessage), Is.True, identityMessage);
                string stageId = CurrentDraftPayload(controller, entryIndex).Stages[0].StageId;
                Assert.That(controller.TryMoveLogicStages(
                    entryIndex,
                    firstIdentity,
                    new[] { stageId },
                    1,
                    1,
                    out _,
                    out string firstMoveMessage), Is.True, firstMoveMessage);
                Assert.That(controller.TryResolveStageEditIdentity(
                    entryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity staleIdentity,
                    out identityMessage), Is.True, identityMessage);
                Assert.That(controller.TryMoveLogicStages(
                    entryIndex,
                    staleIdentity,
                    new[] { stageId },
                    1,
                    1,
                    out _,
                    out string secondMoveMessage), Is.True, secondMoveMessage);
                int revision = controller.DraftWorkspace.CurrentRevision;
                long candidateHash = (controller.CandidateRevision?.AuthorityHash ?? 0L);
                bool canUndo = controller.DraftWorkspace.CanUndo;

                Assert.That(controller.TryUndoBehaviorCardEdit(
                    entryIndex,
                    staleIdentity,
                    out _,
                    out _,
                    out string undoMessage), Is.False);
                StringAssert.Contains("失效", undoMessage);
                Assert.That(controller.DraftWorkspace.CurrentRevision, Is.EqualTo(revision));
                Assert.That((controller.CandidateRevision?.AuthorityHash ?? 0L), Is.EqualTo(candidateHash));
                Assert.That(controller.DraftWorkspace.CanUndo, Is.EqualTo(canUndo));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void StageHistory_RemovedSelectedEntryClearsSelectionAndReturnsBehaviorGroup()
        {
            var host = new GameObject("Behavior Card Missing History Route Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.That(controller.TryCreateReactiveChaseDodgeGroup(0, out string presetMessage),
                    Is.True, presetMessage);
                if (controller.AuthoringMode != CharacterTestRuntimeAiLabAuthoringMode.Developer)
                    controller.ToggleAuthoringMode();
                string[] entryIdsBeforeDuplicate = CurrentEntryIds(controller);
                Assert.That(controller.DraftWorkspace.TryDuplicateEntry(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    2,
                    out string duplicateMessage), Is.True, duplicateMessage);

                int selectedEntryIndex = FindNewEntryIndex(controller, entryIdsBeforeDuplicate);
                Assert.That(selectedEntryIndex, Is.GreaterThanOrEqualTo(0));
                CharacterTestRuntimeAiLabWorkspace sourceWorkspace =
                    controller.EnsureShellCoordinator(playerDeckAvailable: true)
                        .CurrentState.ActiveWorkspace;
                EnterBehaviorCardWorkspace(controller, selectedEntryIndex);
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace =
                    ProjectDraft(controller, selectedEntryIndex);
                var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                handler.Configure(
                    controller,
                    null,
                    null,
                    null,
                    () => selectedEntryIndex,
                    workspaceView: () => workspace);
                string stageId = workspace.Timeline.Stages[0].StageId;
                Assert.That(handler.SelectStage(
                    stageId,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None), Is.True);
                Assert.That(handler.SelectedStageCount, Is.EqualTo(1));

                handler.UndoStageEdit();

                Assert.That(handler.SelectedStageCount, Is.EqualTo(0));
                Assert.That(
                    controller.ShellCoordinator.CurrentState.ActiveWorkspace,
                    Is.EqualTo(sourceWorkspace));
                Assert.That(controller.DraftWorkspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft restoredGroup), Is.True);
                Assert.That(restoredGroup.Entries.Count, Is.EqualTo(entryIdsBeforeDuplicate.Length));
                StringAssert.Contains("已返回来源页面", handler.LastMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static MxUiCommand Command(string commandId, string stableId, int direction = 0)
        {
            return new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                commandId,
                new CharacterTestRuntimeAiLabBehaviorCardElementCommand(stableId, direction));
        }

        private static void ProjectWarmWorkspace(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtime,
            CharacterTestRuntimeAiLabLogicEventBuffer events,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView library,
            CharacterTestRuntimeAiLabBehaviorCardLibraryState libraryState,
            int runtimeFrame)
        {
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Pursuit Dash Strike",
                compile,
                payload,
                0,
                CharacterTestRuntimeAiLabLogicElementKind.Stage,
                runtimeState: runtime,
                latestEvents: events,
                runtimeFrame: runtimeFrame);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.FillBehaviorCardLibrary(
                viewModel,
                library,
                libraryState);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FinalizeBehaviorCardWorkspaceRevision(viewModel);
        }

        private static void ProjectStressWorkspace(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            string displayName,
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView library,
            CharacterTestRuntimeAiLabBehaviorCardLibraryState libraryState)
        {
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                displayName,
                compile,
                payload,
                0,
                CharacterTestRuntimeAiLabLogicElementKind.Stage);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            timeline.HorizontalStartFrame = 180;
            timeline.HorizontalEndFrame = 660;
            timeline.VerticalScrollOffset = 720f;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.FillBehaviorCardLibrary(
                viewModel,
                library,
                libraryState);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FinalizeBehaviorCardWorkspaceRevision(viewModel);
        }

        internal static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreateD1StressFixture()
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "stage.stress.00",
                Intent = new string('意', 128),
                RiskSummary = new string('险', 128)
            };
            for (int i = 0; i < 12; i++)
            {
                string stageId = "stage.stress." + i.ToString("00");
                payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
                {
                    StageId = stageId,
                    Order = i,
                    Kind = CharacterTestRuntimeAiLabCardStepKind.Logic,
                    Title = i == 0 ? new string('阶', 64) : "阶段 " + i.ToString("00"),
                    PlannedStartFrame = i * 90,
                    PlannedDurationFrames = 60,
                    PreviewDurationFrames = 60,
                    DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Fixed,
                    Note = i == 0 ? new string('详', 512) : string.Empty
                });
                payload.Layout.Stages.Add(new CharacterTestRuntimeAiLabLogicStageLayout
                {
                    StageId = stageId,
                    Column = i,
                    Width = 1f
                });
            }

            for (int i = 0; i < 16; i++)
            {
                string detectorId = "detector.card." + i.ToString("00");
                var detector = new CharacterTestRuntimeAiLabLogicDetector
                {
                    DetectorId = detectorId,
                    Title = i == 0 ? new string('检', 128) : "卡级检测 " + i.ToString("00"),
                    Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Card,
                    Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                    Priority = 1000 - i,
                    ActivationStartFrame = i * 15,
                    ActivationDurationFrames = 0,
                    WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite
                };
                detector.Conditions.Add(new CharacterTestRuntimeAiLabCardCondition
                {
                    ConditionId = detectorId + ".visible",
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetVisible,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                    BoolValue = true
                });
                payload.Detectors.Add(detector);
            }
            for (int i = 0; i < 24; i++)
            {
                string detectorId = "detector.stage." + i.ToString("00");
                payload.Detectors.Add(new CharacterTestRuntimeAiLabLogicDetector
                {
                    DetectorId = detectorId,
                    Title = "阶段检测 " + i.ToString("00"),
                    Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                    StageId = "stage.stress." + (i / 2).ToString("00"),
                    Trigger = (i & 1) == 0
                        ? CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted
                        : CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed,
                    Priority = 800 - i,
                    ActivationStartFrame = 0,
                    ActivationDurationFrames = 60,
                    WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd
                });
            }

            int transitionIndex = 0;
            for (int i = 0; i < 11; i++)
                AddStressTransition(payload, ref transitionIndex, i, i + 1, false);
            for (int i = 0; i < 9; i++)
                AddStressTransition(payload, ref transitionIndex, i, i + 2, false);
            for (int i = 0; i < 8; i++)
                AddStressTransition(payload, ref transitionIndex, i + 4, i, false);
            for (int i = 0; i < 4; i++)
                AddStressTransition(payload, ref transitionIndex, i, i, false);
            for (int i = 0; i < 4; i++)
                AddStressTransition(payload, ref transitionIndex, i + 8, -1, true);
            return payload;
        }

        private static void AddStressTransition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            ref int transitionIndex,
            int sourceStage,
            int targetStage,
            bool endsCard)
        {
            string transitionId = "transition.stress." + transitionIndex.ToString("00");
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = transitionId,
                DetectorId = payload.Detectors[transitionIndex].DetectorId,
                SourceStageId = "stage.stress." + sourceStage.ToString("00"),
                TargetStageId = endsCard
                    ? CharacterTestRuntimeAiLabLogicTransition.EndTarget
                    : "stage.stress." + targetStage.ToString("00"),
                Priority = 1000 - transitionIndex,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.AfterActionEnd,
                Label = transitionIndex == 0
                    ? new string('跳', 512)
                    : "压力跳转 " + transitionIndex.ToString("00"),
                EndReason = endsCard ? "压力样例结束" : string.Empty
            });
            transitionIndex++;
        }

        private static string JoinDiagnostics(
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile)
        {
            if (compile?.Diagnostics == null)
                return string.Empty;
            string result = string.Empty;
            for (int i = 0; i < compile.Diagnostics.Count; i++)
                result += compile.Diagnostics[i].Code + ":" + compile.Diagnostics[i].Message + "\n";
            return result;
        }

        private static void NormalizeNativeDrafts(CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(CharacterTestArenaMatchConfig.Team1Id, 0, out var group));
            foreach (var entry in group.Entries)
                CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(
                    controller.DraftWorkspace.Cards[entry.CardDraftId].LogicPayload);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel ProjectDraft(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            int entryIndex)
        {
            Assert.That(
                controller.DraftWorkspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group),
                Is.True);
            CharacterTestRuntimeAiLabBehaviorCardDraft card =
                controller.DraftWorkspace.Cards[group.Entries[entryIndex].CardDraftId];
            var workspace = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel();
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload;
            var stages = new CharacterTestRuntimeAiLabLogicStageViewModel[payload.Stages.Count];
            for (int i = 0; i < stages.Length; i++)
            {
                stages[i] = new CharacterTestRuntimeAiLabLogicStageViewModel
                {
                    Index = i,
                    StageId = payload.Stages[i].StageId,
                    Order = payload.Stages[i].Order,
                    StartFrame = payload.Stages[i].PlannedStartFrame,
                    DurationFrames = payload.Stages[i].PlannedDurationFrames,
                    DurationMode = payload.Stages[i].DurationMode,
                    OwnsPrimaryAction = payload.Stages[i].Kind
                        != CharacterTestRuntimeAiLabCardStepKind.Logic
                };
            }
            var detectors = new CharacterTestRuntimeAiLabLogicDetectorViewModel[payload.Detectors.Count];
            for (int i = 0; i < detectors.Length; i++)
            {
                detectors[i] = new CharacterTestRuntimeAiLabLogicDetectorViewModel
                {
                    Index = i,
                    DetectorId = payload.Detectors[i].DetectorId,
                    Priority = payload.Detectors[i].Priority
                };
            }
            var transitions = new CharacterTestRuntimeAiLabLogicTransitionViewModel[payload.Transitions.Count];
            for (int i = 0; i < transitions.Length; i++)
            {
                transitions[i] = new CharacterTestRuntimeAiLabLogicTransitionViewModel
                {
                    Index = i,
                    TransitionId = payload.Transitions[i].TransitionId
                };
            }
            workspace.Timeline.Stages = stages;
            workspace.Timeline.Detectors = detectors;
            workspace.Timeline.Transitions = transitions;
            workspace.Timeline.StageCount = stages.Length;
            workspace.Timeline.DetectorCount = detectors.Length;
            workspace.Timeline.TransitionCount = transitions.Length;
            workspace.Timeline.CanEdit = controller.CanEditBehaviorCardDraft;
            workspace.Timeline.RequiresDetectorWindowCanonicalization =
                CharacterTestRuntimeAiLabBehaviorCardGraphMutation.RequiresWindowCanonicalization(payload);
            for (int i = 0; i < detectors.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[i];
                detectors[i].Scope = detector.Scope;
                detectors[i].CardScope = detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card;
                detectors[i].StageIndex = FindStageIndex(payload, detector.StageId);
                detectors[i].StartFrame = detector.ActivationStartFrame;
                detectors[i].DurationFrames = detector.ActivationDurationFrames;
                detectors[i].WindowEndMode = detector.WindowEndMode;
            }
            for (int i = 0; i < transitions.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                transitions[i].DetectorIndex = FindDetectorIndex(payload, transition.DetectorId);
                transitions[i].SourceStageIndex = FindStageIndex(payload, transition.SourceStageId);
                transitions[i].TargetStageIndex = FindStageIndex(payload, transition.TargetStageId);
                transitions[i].SourceStageId = transition.SourceStageId;
                transitions[i].TargetStageId = transition.TargetStageId;
                transitions[i].EndsCard = string.Equals(
                    transition.TargetStageId,
                    CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                    StringComparison.Ordinal);
            }
            return workspace;
        }

        private static void RenameFirstTransitionDetector(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId)
        {
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.Transitions.Count, Is.GreaterThan(0));
            int transitionIndex = -1;
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                string candidateDetectorId = payload.Transitions[i]?.DetectorId ?? string.Empty;
                if (!candidateDetectorId.StartsWith("event.guard.", StringComparison.Ordinal))
                {
                    transitionIndex = i;
                    break;
                }
            }
            Assert.That(transitionIndex, Is.GreaterThanOrEqualTo(0));
            string originalDetectorId = payload.Transitions[transitionIndex].DetectorId;
            int detectorIndex = FindDetectorIndex(payload, originalDetectorId);
            Assert.That(detectorIndex, Is.GreaterThanOrEqualTo(0));
            payload.Detectors[detectorIndex].DetectorId = detectorId;
            string eventId = detectorId.StartsWith("event.", StringComparison.Ordinal)
                ? detectorId.Substring("event.".Length)
                : detectorId;
            int renamedCount = 0;
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                if (string.Equals(
                        payload.Transitions[i].DetectorId,
                        originalDetectorId,
                        StringComparison.Ordinal))
                {
                    payload.Transitions[i].DetectorId = detectorId;
                    payload.Transitions[i].TransitionId = renamedCount == 0
                        ? eventId
                        : eventId + "." + renamedCount;
                    renamedCount++;
                }
            }
        }

        private static int FindFirstEditableDetectorIndex(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            for (int i = 0; timeline != null && i < timeline.DetectorCount; i++)
            {
                string detectorId = timeline.Detectors[i]?.DetectorId ?? string.Empty;
                if (!detectorId.StartsWith("event.guard.", StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static int FindFirstEditableTransitionIndex(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            for (int i = 0; timeline != null && i < timeline.TransitionCount; i++)
            {
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition = timeline.Transitions[i];
                if (transition == null
                    || transition.DetectorIndex < 0
                    || transition.DetectorIndex >= timeline.DetectorCount)
                {
                    continue;
                }
                string detectorId = timeline.Detectors[transition.DetectorIndex]?.DetectorId
                    ?? string.Empty;
                if (!detectorId.StartsWith("event.guard.", StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static int FindDetectorIndex(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            string detectorId)
        {
            for (int i = 0; timeline != null && i < timeline.DetectorCount; i++)
            {
                if (string.Equals(
                        timeline.Detectors[i]?.DetectorId,
                        detectorId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        private static int FindTransitionIndexForDetector(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            string detectorId)
        {
            int detectorIndex = FindDetectorIndex(timeline, detectorId);
            for (int i = 0; timeline != null && i < timeline.TransitionCount; i++)
            {
                if (timeline.Transitions[i]?.DetectorIndex == detectorIndex)
                    return i;
            }
            return -1;
        }

        private static CharacterTestRuntimeAiLabLogicTransition FindTransitionForDetector(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId)
        {
            for (int i = 0; payload != null && i < payload.Transitions.Count; i++)
            {
                if (string.Equals(
                        payload.Transitions[i]?.DetectorId,
                        detectorId,
                        StringComparison.Ordinal))
                {
                    return payload.Transitions[i];
                }
            }
            return null;
        }

        private static int FindStageIndex(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId)
        {
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                if (string.Equals(payload.Stages[i].StageId, stageId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static int FindDetectorIndex(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId)
        {
            for (int i = 0; i < payload.Detectors.Count; i++)
            {
                if (string.Equals(payload.Detectors[i].DetectorId, detectorId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CurrentDraftPayload(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            int entryIndex)
        {
            Assert.That(controller.DraftWorkspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft group), Is.True);
            return controller.DraftWorkspace.Cards[group.Entries[entryIndex].CardDraftId].LogicPayload;
        }

        private static string[] CurrentEntryIds(
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            Assert.That(controller.DraftWorkspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft group), Is.True);
            var ids = new string[group.Entries.Count];
            for (int i = 0; i < group.Entries.Count; i++)
                ids[i] = group.Entries[i]?.EntryId ?? string.Empty;
            return ids;
        }

        private static int FindNewEntryIndex(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            string[] previousEntryIds)
        {
            string[] current = CurrentEntryIds(controller);
            for (int i = 0; i < current.Length; i++)
            {
                bool existed = false;
                for (int previousIndex = 0;
                    previousEntryIds != null && previousIndex < previousEntryIds.Length;
                    previousIndex++)
                {
                    if (!string.Equals(
                            current[i],
                            previousEntryIds[previousIndex],
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    existed = true;
                    break;
                }
                if (!existed)
                    return i;
            }
            return -1;
        }

        private static int FindEntryIndex(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            string entryId)
        {
            string[] current = CurrentEntryIds(controller);
            for (int i = 0; i < current.Length; i++)
            {
                if (string.Equals(current[i], entryId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel CreateProjectedWorkspace(
            int cardCount)
        {
            var workspace = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel();
            var cards = new CharacterTestRuntimeAiLabBehaviorAssetCardViewModel[cardCount];
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = new CharacterTestRuntimeAiLabBehaviorAssetCardViewModel
                {
                    StableCardId = "card." + i.ToString("00"),
                    Title = "Card " + i.ToString("00"),
                    Available = true
                };
            }
            workspace.Library.Cards = cards;
            workspace.Library.ActiveCount = cards.Length;
            workspace.Library.Available = true;
            return workspace;
        }

        private static void EnterBehaviorCardWorkspace(
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                controller.EnsureShellCoordinator(playerDeckAvailable: true);
            Assert.That(coordinator.AttachMountAdapter(
                coordinator.CurrentState.Generation,
                new BehaviorCardMountAdapter()).Success, Is.True);
            CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
            CharacterTestRuntimeAiLabNavigationResult opened = coordinator.NavigateToBehaviorCard(
                state.Generation,
                state.Revision,
                state.ActiveWorkspace,
                "group.test",
                "entry.test",
                "card.test",
                CharacterTestRuntimeAiLabSelectedElementKind.None,
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorCardViewport.Default);
            Assert.That(opened.Success, Is.True, opened.Message);
        }

        private static void EnterBehaviorCardWorkspace(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            int entryIndex)
        {
            Assert.That(controller.TryResolveStageEditIdentity(
                entryIndex,
                out CharacterTestRuntimeAiLabStageEditIdentity identity,
                out string identityMessage), Is.True, identityMessage);
            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                controller.EnsureShellCoordinator(playerDeckAvailable: true);
            Assert.That(coordinator.AttachMountAdapter(
                coordinator.CurrentState.Generation,
                new BehaviorCardMountAdapter()).Success, Is.True);
            CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
            CharacterTestRuntimeAiLabNavigationResult opened = coordinator.NavigateToBehaviorCard(
                state.Generation,
                state.Revision,
                state.ActiveWorkspace,
                identity.GroupDraftId,
                identity.EntryId,
                identity.CardDraftId,
                CharacterTestRuntimeAiLabSelectedElementKind.None,
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorCardViewport.Default);
            Assert.That(opened.Success, Is.True, opened.Message);
        }

        private sealed class BehaviorCardMountAdapter : ICharacterTestRuntimeAiLabShellMountAdapter
        {
            public bool IsSetupPhase => true;
            public bool HasRuntimeHistory => true;

            public bool TryPrepareForReopen(out string diagnostic)
            {
                diagnostic = string.Empty;
                return true;
            }

            public bool IsWorkspaceAvailable(CharacterTestRuntimeAiLabWorkspace workspace) => true;

            public bool TryNormalizeWorkspaceContext(
                CharacterTestRuntimeAiLabWorkspace workspace,
                CharacterTestRuntimeAiLabPageContext context,
                out CharacterTestRuntimeAiLabPageContext normalizedContext,
                out string diagnostic)
            {
                normalizedContext = context;
                diagnostic = string.Empty;
                return true;
            }

            public bool TryMountWorkspace(
                CharacterTestRuntimeAiLabShellState state,
                out string diagnostic)
            {
                diagnostic = string.Empty;
                return true;
            }

            public bool TryMountTransition(
                CharacterTestRuntimeAiLabShellState state,
                out string diagnostic)
            {
                diagnostic = string.Empty;
                return true;
            }

            public bool TryMountRecovery(
                CharacterTestRuntimeAiLabShellState state,
                out string diagnostic)
            {
                diagnostic = string.Empty;
                return true;
            }

            public bool TryDismissTopModalForVisibilityToggle(
                out bool dismissed,
                out string diagnostic)
            {
                dismissed = false;
                diagnostic = string.Empty;
                return true;
            }

            public void UnmountAll()
            {
            }
        }

        private static CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView CreateWorkbenchView()
        {
            var cards = new[]
            {
                new CharacterTestRuntimeAiPlannerPlayerDeckCardView(
                    "card.alpha",
                    null,
                    CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available,
                    true,
                    true,
                    false,
                    true,
                    false,
                    false,
                    CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.AlreadyInstalled),
                new CharacterTestRuntimeAiPlannerPlayerDeckCardView(
                    "card.beta",
                    null,
                    CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Incompatible,
                    true,
                    false,
                    false,
                    false,
                    true,
                    false,
                    CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.Incompatible)
            };
            return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView(
                "character.test",
                1,
                1,
                true,
                123L,
                false,
                cards,
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                false);
        }

        private static void AssertHistoricalSelectionDoesNotMutateLatest(
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler handler,
            CharacterTestRuntimeAiLabShellCoordinator coordinator,
            string commandId,
            string stableId,
            CharacterTestRuntimeAiLabPageContext latest)
        {
            Assert.That(handler.TryHandle(
                new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    commandId,
                    new CharacterTestRuntimeAiLabBehaviorCardElementCommand(stableId)),
                out _), Is.True);
            AssertLatestSelectionUnchanged(coordinator, latest);
        }

        private static void NavigateAndAssertLatestSelection(
            CharacterTestRuntimeAiLabShellCoordinator coordinator,
            CharacterTestRuntimeAiLabWorkspace workspace,
            CharacterTestRuntimeAiLabPageContext latest)
        {
            CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
            CharacterTestRuntimeAiLabNavigationResult result = coordinator.Navigate(
                state.Generation,
                state.Revision,
                state.ActiveWorkspace,
                workspace);
            Assert.That(result.Success, Is.True, result.Message);
            AssertLatestSelectionUnchanged(coordinator, latest);
        }

        private static void AssertLatestSelectionUnchanged(
            CharacterTestRuntimeAiLabShellCoordinator coordinator,
            CharacterTestRuntimeAiLabPageContext latest)
        {
            CharacterTestRuntimeAiLabPageContext actual = coordinator.CurrentState.PageContext;
            Assert.That(actual.GroupDraftId, Is.EqualTo(latest.GroupDraftId));
            Assert.That(actual.EntryId, Is.EqualTo(latest.EntryId));
            Assert.That(actual.CardDraftId, Is.EqualTo(latest.CardDraftId));
            Assert.That(actual.SelectedElementKind, Is.EqualTo(latest.SelectedElementKind));
            Assert.That(actual.SelectedElementStableId, Is.EqualTo(latest.SelectedElementStableId));
        }

        private static long ComputeBehaviorCardSourceRevision(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            long presentationCommandRevision = 0L)
        {
            return CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .ComputeBehaviorCardBuildSourceRevision(
                    controller,
                    string.Empty,
                    null,
                    0,
                    0,
                    CharacterTestRuntimeAiLabLogicElementKind.Stage,
                    0,
                    null,
                    null,
                    presentationCommandRevision);
        }
    }
}
