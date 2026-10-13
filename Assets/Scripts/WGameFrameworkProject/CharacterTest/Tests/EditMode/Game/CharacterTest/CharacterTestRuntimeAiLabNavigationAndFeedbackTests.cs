using System;
using System.IO;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiLabNavigationAndFeedbackTests
    {
        [Test]
        public void NoChanges_ReturnsWithoutFlush()
        {
            int flushCount = 0;
            CharacterTestRuntimeAiLabNavigationExitResult result = Evaluate(
                draftDirty: false,
                characterActionDirty: false,
                operationBusy: false,
                () =>
                {
                    flushCount++;
                    return new CharacterTestRuntimeAiLabNavigationFlushResult(true, string.Empty);
                });

            Assert.IsTrue(result.CanProceed);
            Assert.IsFalse(result.FlushAttempted);
            Assert.AreEqual(0, flushCount);
        }

        [Test]
        public void PendingAutosave_FlushesExactlyOnceThenReturns()
        {
            int flushCount = 0;
            CharacterTestRuntimeAiLabNavigationExitResult result = Evaluate(
                draftDirty: true,
                characterActionDirty: false,
                operationBusy: false,
                () =>
                {
                    flushCount++;
                    return new CharacterTestRuntimeAiLabNavigationFlushResult(true, "saved");
                });

            Assert.IsTrue(result.CanProceed);
            Assert.IsTrue(result.FlushAttempted);
            Assert.AreEqual(1, flushCount);
        }

        [Test]
        public void SaveFailure_StaysInPlaceAndSeparatesTechnicalDetails()
        {
            int flushCount = 0;
            const string technical = "root=C:/private/draft; stableId=card-1; IOException";
            CharacterTestRuntimeAiLabNavigationExitResult result = Evaluate(
                draftDirty: true,
                characterActionDirty: false,
                operationBusy: false,
                () =>
                {
                    flushCount++;
                    return new CharacterTestRuntimeAiLabNavigationFlushResult(false, technical);
                });

            Assert.AreEqual(
                CharacterTestRuntimeAiLabNavigationExitDisposition.SaveFailed,
                result.Disposition);
            Assert.AreEqual(1, flushCount);
            StringAssert.Contains("仍停留", result.Feedback.BuildPrimaryText());
            StringAssert.DoesNotContain("stableId", result.Feedback.BuildPrimaryText());
            Assert.AreEqual(technical, result.Feedback.TechnicalDetails);
            CollectionAssert.Contains(
                result.Feedback.Actions,
                CharacterTestRuntimeAiLabNavigationFeedbackAction.RetrySave);
            CollectionAssert.Contains(
                result.Feedback.Actions,
                CharacterTestRuntimeAiLabNavigationFeedbackAction.DiscardPendingChanges);
            CollectionAssert.Contains(
                result.Feedback.Actions,
                CharacterTestRuntimeAiLabNavigationFeedbackAction.Cancel);
        }

        [Test]
        public void TestOrPublishBusy_FailsClosedWithoutFlush()
        {
            int flushCount = 0;
            CharacterTestRuntimeAiLabNavigationExitResult result = Evaluate(
                draftDirty: true,
                characterActionDirty: false,
                operationBusy: true,
                () =>
                {
                    flushCount++;
                    return new CharacterTestRuntimeAiLabNavigationFlushResult(true, string.Empty);
                });

            Assert.AreEqual(
                CharacterTestRuntimeAiLabNavigationExitDisposition.Busy,
                result.Disposition);
            Assert.AreEqual(0, flushCount);
            StringAssert.Contains("请等待", result.Feedback.BuildPrimaryText());
            CollectionAssert.Contains(
                result.Feedback.Actions,
                CharacterTestRuntimeAiLabNavigationFeedbackAction.Cancel);
        }

        [Test]
        public void CharacterActionClean_ReturnsWithoutFalseUnsavedChoice()
        {
            CharacterTestRuntimeAiLabNavigationExitResult result =
                CharacterTestRuntimeAiLabNavigationExitPolicy.Evaluate(
                    CharacterTestRuntimeAiLabNavigationExitIntent.CharacterActionReturn,
                    new CharacterTestRuntimeAiLabNavigationExitState(
                        historicalReadOnly: false,
                        operationBusy: false,
                        busyStage: string.Empty,
                        draftDirty: false,
                        canWriteDraft: true,
                        characterActionOpen: true,
                        characterActionDirty: false),
                    null);

            Assert.IsTrue(result.CanProceed);
            Assert.IsFalse(result.Feedback.IsVisible);
        }

        [Test]
        public void CharacterActionDirty_RequiresExplicitThreeWayChoice()
        {
            CharacterTestRuntimeAiLabNavigationExitResult result =
                CharacterTestRuntimeAiLabNavigationExitPolicy.Evaluate(
                    CharacterTestRuntimeAiLabNavigationExitIntent.CharacterActionReturn,
                    new CharacterTestRuntimeAiLabNavigationExitState(
                        historicalReadOnly: false,
                        operationBusy: false,
                        busyStage: string.Empty,
                        draftDirty: false,
                        canWriteDraft: true,
                        characterActionOpen: true,
                        characterActionDirty: true),
                    null);

            Assert.AreEqual(
                CharacterTestRuntimeAiLabNavigationExitDisposition.CharacterActionChoiceRequired,
                result.Disposition);
            CollectionAssert.AreEqual(
                new[]
                {
                    CharacterTestRuntimeAiLabNavigationFeedbackAction.SaveCharacterActionAndReturn,
                    CharacterTestRuntimeAiLabNavigationFeedbackAction.DiscardCharacterActionAndReturn,
                    CharacterTestRuntimeAiLabNavigationFeedbackAction.Cancel,
                },
                result.Feedback.Actions);
        }

        [Test]
        public void Discard_RestoresSavedTempFixtureWithoutDeletingIt()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "MxFrameworkTests",
                "RuntimeAiLabNavigationAndFeedback",
                Guid.NewGuid().ToString("N"));
            try
            {
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(
                    Path.Combine(root, "Drafts"),
                    Path.Combine(root, "Recovery"));
                CharacterTestRuntimeAiLabDraftWorkspace workspace =
                    persistence.CreateFromCurrent(CharacterTestArenaMatchConfig.CreateDefault());
                workspace.DisplayName = "saved-name";
                Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
                string workspaceDirectory = Path.Combine(
                    persistence.DraftsRoot,
                    workspace.WorkspaceId);

                workspace.DisplayName = "unsaved-name";
                workspace.CurrentRevision++;
                persistence.MarkDirty();
                Assert.IsTrue(workspace.Dirty);

                Assert.IsTrue(
                    persistence.TryDiscardUnsavedChanges(
                        out CharacterTestRuntimeAiLabDraftWorkspace restored,
                        out string diagnostic),
                    diagnostic);
                Assert.AreEqual("saved-name", restored.DisplayName);
                Assert.IsFalse(restored.Dirty);
                Assert.IsTrue(Directory.Exists(workspaceDirectory));
                StringAssert.StartsWith(Path.GetFullPath(root), persistence.DraftsRoot);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        private static CharacterTestRuntimeAiLabNavigationExitResult Evaluate(
            bool draftDirty,
            bool characterActionDirty,
            bool operationBusy,
            Func<CharacterTestRuntimeAiLabNavigationFlushResult> flush)
        {
            return CharacterTestRuntimeAiLabNavigationExitPolicy.Evaluate(
                CharacterTestRuntimeAiLabNavigationExitIntent.WorkspaceBack,
                new CharacterTestRuntimeAiLabNavigationExitState(
                    historicalReadOnly: false,
                    operationBusy,
                    busyStage: operationBusy ? "发布当前修改" : string.Empty,
                    draftDirty,
                    canWriteDraft: true,
                    characterActionOpen: characterActionDirty,
                    characterActionDirty),
                flush);
        }
    }
}
