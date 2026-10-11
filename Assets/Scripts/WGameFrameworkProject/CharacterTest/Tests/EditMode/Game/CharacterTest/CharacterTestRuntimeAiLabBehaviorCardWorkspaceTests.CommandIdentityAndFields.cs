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
        public void PageScopedCommandsResolveStableIdsAndRejectStaleTokens()
        {
            var host = new GameObject("Behavior Card Stable Command Test");
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
                int selectedIndex = -1;
                CharacterTestRuntimeAiLabLogicElementKind selectedKind =
                    CharacterTestRuntimeAiLabLogicElementKind.None;
                var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                handler.Configure(
                    controller,
                    null,
                    null,
                    () => workspace = ProjectDraft(controller, entryIndex),
                    () => entryIndex,
                    (kind, index) =>
                    {
                        selectedKind = kind;
                        selectedIndex = index;
                    },
                    () => workspace);

                string firstStageId = workspace.Timeline.Stages[0].StageId;
                Assert.That(handler.TryHandle(
                    Command(CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectStage, firstStageId),
                    out _), Is.True);
                Assert.That(selectedKind, Is.EqualTo(CharacterTestRuntimeAiLabLogicElementKind.Stage));
                Assert.That(selectedIndex, Is.EqualTo(0));

                Assert.That(handler.TryHandle(
                    Command(CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectStage, "stage.stale"),
                    out _), Is.True);
                Assert.That(selectedIndex, Is.EqualTo(0));
                StringAssert.Contains("失效", handler.LastMessage);

                CharacterTestRuntimeAiLabLogicStageViewModel[] highWaterStages =
                    workspace.Timeline.Stages;
                Array.Resize(ref highWaterStages, workspace.Timeline.StageCount + 1);
                workspace.Timeline.Stages = highWaterStages;
                highWaterStages[workspace.Timeline.StageCount] =
                    new CharacterTestRuntimeAiLabLogicStageViewModel
                    {
                        Index = workspace.Timeline.StageCount,
                        StageId = "stage.high-water.stale"
                    };
                Assert.That(handler.TryHandle(
                    Command(
                        CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectStage,
                        "stage.high-water.stale"),
                    out _), Is.True);
                Assert.That(selectedIndex, Is.EqualTo(0));
                StringAssert.Contains("失效", handler.LastMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NativeEventPriorityEditRejectsStaleAuthority()
        {

            using var f = new NativeEditingFixture();
            int revision = f.Controller.DraftWorkspace.CurrentRevision;
            int priority = f.Event().Priority;
            f.Edit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Priority,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: priority + 20));
            Assert.AreEqual(priority + 20, f.Event().Priority);
            Assert.AreEqual(revision + 1, f.Controller.DraftWorkspace.CurrentRevision);
            var stale = f.Identity();
            f.Edit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(stringValue: "current"));
            Assert.IsFalse(f.Controller.TryApplyBehaviorRecipeTriggerEventField(NativeEditingFixture.Entry, stale,
                "approach", "event.exit", CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Priority, -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: priority), out _, out _));
            Assert.AreEqual(priority + 20, f.Event().Priority);
        }

        [Test]
        public void NativeEventEditsAndDeletesRequireFullCardIdentity()
        {

            using var f = new NativeEditingFixture();
            var wrongCard = f.Identity();
            Assert.AreNotEqual(wrongCard.CardDraftId, f.Identity(1).CardDraftId);
            string first = f.Event().Title, second = f.Event(1).Title;
            int revision = f.Controller.DraftWorkspace.CurrentRevision;
            Assert.IsFalse(f.Controller.TryApplyBehaviorRecipeTriggerEventField(1, wrongCard,
                "approach", "event.exit", CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title, -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(stringValue: "wrong card"), out _, out _));
            Assert.AreEqual(first, f.Event().Title);
            Assert.AreEqual(second, f.Event(1).Title);
            Assert.AreEqual(revision, f.Controller.DraftWorkspace.CurrentRevision);
            Assert.IsFalse(f.Controller.TryDeleteBehaviorRecipeTriggerEvent(1, wrongCard, "approach", "event.exit", out _, out _));
            Assert.NotNull(f.Event()); Assert.NotNull(f.Event(1));
            f.Edit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(stringValue: "right card"));
            Assert.AreEqual("right card", f.Event().Title);
            Assert.AreEqual(second, f.Event(1).Title);
        }

        [Test]
        public void StageFieldNoOp_CompletesEditSessionWithoutCreatingHistory()
        {
            var host = new GameObject("Behavior Card Stage NoOp Test");
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
                CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload =
                    CurrentDraftPayload(controller, entryIndex);
                string unchangedTitle = payload.Stages[0].Title;
                int revision = controller.DraftWorkspace.CurrentRevision;

                Assert.That(handler.BeginStageFieldEdit(
                    stageId,
                    CharacterTestRuntimeAiLabStageFieldKind.Title,
                    out long epoch), Is.True);
                Assert.That(handler.CommitStageFieldEdit(
                    epoch,
                    stageId,
                    CharacterTestRuntimeAiLabStageFieldKind.Title,
                    new CharacterTestRuntimeAiLabStageFieldValue(stringValue: unchangedTitle)), Is.True);

                Assert.That(handler.HasActiveStageFieldEdit, Is.False);
                Assert.That(controller.DraftWorkspace.CurrentRevision, Is.EqualTo(revision));
                StringAssert.Contains("没有变化", handler.LastMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NativeEventFieldCommitKeepsIdentityAndCreatesOneUndoableRevision()
        {

            using var f = new NativeEditingFixture();
            int revision = f.Controller.DraftWorkspace.CurrentRevision;
            string original = f.Event().Title, conditionId = f.Event().ConditionRoot.NodeId;
            f.Edit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(stringValue: "Threat Gate"));
            Assert.AreEqual("Threat Gate", f.Event().Title);
            Assert.AreEqual("event.exit", f.Event().EventId);
            Assert.AreEqual(conditionId, f.Event().ConditionRoot.NodeId);
            Assert.AreEqual(revision + 1, f.Controller.DraftWorkspace.CurrentRevision);
            Assert.IsEmpty(f.Payload().Detectors, "Native saves do not recreate a writable Detector projection.");
            f.Undo();
            Assert.AreEqual(original, f.Event().Title);
        }

        [Test]
        public void NativeEventFieldCommitRejectsRevisionChangedByEventMutation()
        {

            using var f = new NativeEditingFixture();
            var stale = f.Identity();
            f.AddEvent();
            int revision = f.Controller.DraftWorkspace.CurrentRevision;
            string title = f.Event().Title;
            Assert.IsFalse(f.Controller.TryApplyBehaviorRecipeTriggerEventField(NativeEditingFixture.Entry, stale,
                "approach", "event.exit", CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title, -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(stringValue: "stale"), out _, out _));
            Assert.AreEqual(title, f.Event().Title);
            Assert.AreEqual(revision, f.Controller.DraftWorkspace.CurrentRevision);
        }

        [Test]
        public void NativeEventTimingChangeCanonicalizesOneUndoableTransaction()
        {

            using var f = new NativeEditingFixture();
            int revision = f.Controller.DraftWorkspace.CurrentRevision;
            int duration = f.Event().DurationFrames;
            f.Edit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.TimingKind,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant));
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant, f.Event().TimingKind);
            Assert.AreEqual(1, f.Event().DurationFrames);
            Assert.AreEqual(1, f.Event().MaxExecutionsPerPass);
            Assert.AreEqual(0, f.Event().CooldownFrames);
            Assert.AreEqual(revision + 1, f.Controller.DraftWorkspace.CurrentRevision);
            f.Undo();
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration, f.Event().TimingKind);
            Assert.AreEqual(duration, f.Event().DurationFrames);
        }

        [Test]
        public void DetectorWindowCanonicalizationConfirmationCancelsWhenDraftIdentityChanges()
        {
            var host = new GameObject("Behavior Card Detector Canonicalization Identity Test");
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
                CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload =
                    CurrentDraftPayload(controller, entryIndex);
                payload.Detectors[0].WindowEndMode =
                    CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.UntilEvent;
                payload.Detectors[0].ActivationDurationFrames = 33;
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace =
                    ProjectDraft(controller, entryIndex);
                var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                handler.Configure(
                    controller,
                    null,
                    null,
                    null,
                    () => entryIndex,
                    workspaceView: () => workspace);
                MxUiCommand canonicalize = Command(
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CanonicalizeDetectorWindows,
                    string.Empty);

                Assert.That(handler.TryHandle(canonicalize, out _), Is.True);
                Assert.That(handler.CanonicalizationConfirmationPending, Is.True);
                Assert.That(controller.TryResolveStageEditIdentity(
                    entryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity identity,
                    out string identityMessage), Is.True, identityMessage);
                Assert.That(controller.TryCanonicalizeLogicDetectorWindows(
                    entryIndex,
                    identity,
                    out _,
                    out string canonicalizeMessage), Is.True, canonicalizeMessage);

                Assert.That(handler.TryHandle(canonicalize, out _), Is.True);
                Assert.That(handler.CanonicalizationConfirmationPending, Is.False);
                StringAssert.Contains("身份或 revision 改变", handler.LastMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CandidateApplyWindowGateOnlyScansCardsReferencedBySelectedGroup()
        {
            var host = new GameObject("Behavior Card Candidate Apply Scope Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.That(controller.TryCreateReactiveChaseDodgeGroup(0, out string presetMessage),
                    Is.True, presetMessage);

                Assert.That(controller.DraftWorkspace.TryGetGroupForSlot(
                    controller.SelectedSetupTeamId,
                    controller.SelectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft selectedGroup), Is.True);
                for (int entryIndex = 0; entryIndex < selectedGroup.Entries.Count; entryIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = selectedGroup.Entries[entryIndex];
                    Assert.That(controller.DraftWorkspace.Cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft referencedCard), Is.True);
                    Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation
                        .TryCanonicalizeDetectorWindows(
                            referencedCard.LogicPayload,
                            out _,
                            out string canonicalizationDiagnostic),
                        Is.True,
                        canonicalizationDiagnostic);
                }

                CharacterTestRuntimeAiLabBehaviorCardDraft orphan =
                    new CharacterTestRuntimeAiLabBehaviorCardDraft(
                        null,
                        "orphan.legacy-window",
                        "Orphan",
                        "Test",
                        new CharacterTestRuntimeAiLabBehaviorCardPayload());
                orphan.LogicPayload = CurrentDraftPayload(controller, 2).Clone();
                orphan.LogicPayload.Detectors[0].WindowEndMode =
                    CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.UntilEvent;
                orphan.LogicPayload.Detectors[0].ActivationDurationFrames = 44;
                controller.DraftWorkspace.AddLoadedCard(orphan);

                Assert.That(controller.QueueCandidateApply(), Is.False);
                StringAssert.DoesNotContain(
                    "DETECTOR_WINDOW_CANONICALIZATION_REQUIRED",
                    controller.StatusMessage);

                CharacterTestRuntimeAiLabBehaviorCardLogicPayload referenced =
                    CurrentDraftPayload(controller, 2);
                referenced.Detectors[0].WindowEndMode =
                    CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.UntilEvent;
                referenced.Detectors[0].ActivationDurationFrames = 55;
                Assert.That(controller.QueueCandidateApply(), Is.False);
                StringAssert.Contains(
                    "DETECTOR_WINDOW_CANONICALIZATION_REQUIRED",
                    controller.StatusMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DetectorWindowCanonicalizationGate_BlocksUnrelatedStageAndGraphMutations()
        {
            var host = new GameObject("Behavior Card Detector Canonicalization Gate Test");
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
                CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload =
                    CurrentDraftPayload(controller, entryIndex);
                payload.Detectors[0].WindowEndMode =
                    CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.UntilEvent;
                payload.Detectors[0].ActivationDurationFrames = 15;
                Assert.That(controller.TryResolveStageEditIdentity(
                    entryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity identity,
                    out string identityMessage), Is.True, identityMessage);
                int revision = controller.DraftWorkspace.CurrentRevision;
                int stageCount = payload.Stages.Count;

                Assert.That(controller.TryAddLogicStage(entryIndex, 0, out string addMessage), Is.False);
                StringAssert.Contains("CANONICALIZATION_REQUIRED", addMessage);
                Assert.That(controller.TryEditLogicDetectorField(
                    entryIndex,
                    identity,
                    payload.Detectors[0].DetectorId,
                    CharacterTestRuntimeAiLabDetectorFieldKind.Title,
                    new CharacterTestRuntimeAiLabDetectorFieldValue(stringValue: "Must Not Commit"),
                    out _,
                    out string graphMessage), Is.False);
                StringAssert.Contains("CANONICALIZATION_REQUIRED", graphMessage);
                Assert.That(controller.DraftWorkspace.CurrentRevision, Is.EqualTo(revision));
                Assert.That(payload.Stages.Count, Is.EqualTo(stageCount));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
