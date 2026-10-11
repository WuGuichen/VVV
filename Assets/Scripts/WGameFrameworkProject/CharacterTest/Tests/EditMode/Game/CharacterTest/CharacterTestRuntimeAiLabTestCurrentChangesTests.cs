using System;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiLabTestCurrentChangesTests
    {
        [Test]
        public void Queue_ConsumesExactlyOnceAndRejectsDuplicate()
        {
            var queue = new CharacterTestRuntimeAiLabTestCurrentChangesQueue();
            CharacterTestRuntimeAiLabTestCurrentChangesRequest request = CreateRequest("op-1", 3);

            Assert.That(queue.TryEnqueue(request, out string first), Is.True, first);
            Assert.That(queue.TryEnqueue(request, out string duplicate), Is.False);
            StringAssert.Contains("队列", duplicate);
            Assert.That(queue.TryTake(out CharacterTestRuntimeAiLabTestCurrentChangesRequest taken), Is.True);
            Assert.That(taken.OperationId, Is.EqualTo("op-1"));
            Assert.That(queue.TryTake(out _), Is.False);
        }

        [Test]
        public void Transaction_SaveFailureStopsBeforeValidationOrRestart()
        {
            var tracker = new CharacterTestRuntimeAiLabTestCurrentChangesOperationTracker();
            CharacterTestRuntimeAiLabTestCurrentChangesRequest request = CreateRequest("save-fail", 4);
            Assert.That(tracker.TryBegin(request, out string begin), Is.True, begin);
            int applyCount = 0;

            bool Flush(out string diagnostic)
            {
                diagnostic = "fixture write failure";
                return false;
            }
            CharacterTestRuntimeAiLabTestCurrentChangesApplyResult Apply(Action markRestarting)
            {
                applyCount++;
                markRestarting();
                return Success();
            }

            CharacterTestRuntimeAiLabTestCurrentChangesResult result =
                CharacterTestRuntimeAiLabTestCurrentChangesTransaction.Execute(
                    tracker,
                    Flush,
                    Apply);

            Assert.That(result.Success, Is.False);
            Assert.That(
                result.FailureStage,
                Is.EqualTo(CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Save));
            Assert.That(applyCount, Is.Zero);
            Assert.That(tracker.State,
                Is.EqualTo(CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Failed));
            StringAssert.Contains("当前测试没有改变", result.Message);
        }

        [Test]
        public void Transaction_ValidationFailureNeverReportsCurrentChangesAsApplied()
        {
            var tracker = new CharacterTestRuntimeAiLabTestCurrentChangesOperationTracker();
            CharacterTestRuntimeAiLabTestCurrentChangesRequest request = CreateRequest("invalid", 5);
            Assert.That(tracker.TryBegin(request, out string begin), Is.True, begin);
            bool restartEntered = false;

            bool Flush(out string diagnostic)
            {
                diagnostic = string.Empty;
                return true;
            }
            CharacterTestRuntimeAiLabTestCurrentChangesApplyResult Apply(Action markRestarting)
            {
                return CharacterTestRuntimeAiLabTestCurrentChangesApplyResult.Failed(
                    CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Validation,
                    "TEST_VALIDATION_FAILED",
                    "validation blocked",
                    blockingDiagnosticCount: 2);
            }

            CharacterTestRuntimeAiLabTestCurrentChangesResult result =
                CharacterTestRuntimeAiLabTestCurrentChangesTransaction.Execute(
                    tracker,
                    Flush,
                    Apply);

            Assert.That(restartEntered, Is.False);
            Assert.That(result.Success, Is.False);
            Assert.That(result.BlockingDiagnosticCount, Is.EqualTo(2));
            Assert.That(
                tracker.ResolveCurrentTestSource(
                    sessionRunning: true,
                    runtimeAuthorityHash: 10L,
                    currentWorkspaceId: request.WorkspaceId,
                    currentWorkspaceRevision: request.WorkspaceRevision,
                    currentCandidateAuthorityHash: 10L),
                Is.EqualTo(CharacterTestRuntimeAiLabCurrentTestSource.Unknown));
        }

        [Test]
        public void Transaction_SuccessRecordsNewBaselineAndSourceBecomesStaleAfterEditing()
        {
            var tracker = new CharacterTestRuntimeAiLabTestCurrentChangesOperationTracker();
            CharacterTestRuntimeAiLabTestCurrentChangesRequest request = CreateRequest("success", 6);
            Assert.That(tracker.TryBegin(request, out string begin), Is.True, begin);
            bool restartEntered = false;

            bool Flush(out string diagnostic)
            {
                diagnostic = string.Empty;
                return true;
            }
            CharacterTestRuntimeAiLabTestCurrentChangesApplyResult Apply(Action markRestarting)
            {
                markRestarting();
                restartEntered = true;
                return Success();
            }

            CharacterTestRuntimeAiLabTestCurrentChangesResult result =
                CharacterTestRuntimeAiLabTestCurrentChangesTransaction.Execute(
                    tracker,
                    Flush,
                    Apply);

            Assert.That(restartEntered, Is.True);
            Assert.That(result.Success, Is.True);
            Assert.That(result.RestartedFromFrameZero, Is.True);
            Assert.That(result.FirstVisibleFrame, Is.EqualTo(3),
                "Bootstrap frames may be hidden; the frame-zero flag represents a new session baseline.");
            Assert.That(tracker.State,
                Is.EqualTo(CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Succeeded));
            Assert.That(
                tracker.ResolveCurrentTestSource(
                    true,
                    result.InstalledAuthorityHash,
                    request.WorkspaceId,
                    request.WorkspaceRevision,
                    result.CandidateAuthorityHash),
                Is.EqualTo(CharacterTestRuntimeAiLabCurrentTestSource.UsingCurrentChanges));
            Assert.That(
                tracker.ResolveCurrentTestSource(
                    true,
                    result.InstalledAuthorityHash,
                    request.WorkspaceId,
                    request.WorkspaceRevision + 1,
                    result.CandidateAuthorityHash + 1),
                Is.EqualTo(CharacterTestRuntimeAiLabCurrentTestSource.CurrentChangesUpdated));
        }

        [Test]
        public void Transaction_ExactRecoveryFailureRequiresExplicitRecovery()
        {
            var tracker = new CharacterTestRuntimeAiLabTestCurrentChangesOperationTracker();
            CharacterTestRuntimeAiLabTestCurrentChangesRequest request =
                CreateRequest("recovery-failed", 7);
            Assert.That(tracker.TryBegin(request, out string begin), Is.True, begin);

            bool Flush(out string diagnostic)
            {
                diagnostic = string.Empty;
                return true;
            }
            CharacterTestRuntimeAiLabTestCurrentChangesApplyResult Apply(Action markRestarting)
            {
                markRestarting();
                return CharacterTestRuntimeAiLabTestCurrentChangesApplyResult.Failed(
                    CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Recovery,
                    "TEST_RESTART_RECOVERY_REQUIRED",
                    "旧测试未能精确恢复。",
                    requiresRecovery: true);
            }

            CharacterTestRuntimeAiLabTestCurrentChangesResult result =
                CharacterTestRuntimeAiLabTestCurrentChangesTransaction.Execute(
                    tracker,
                    Flush,
                    Apply);

            Assert.That(result.Success, Is.False);
            Assert.That(result.RequiresRecovery, Is.True);
            Assert.That(result.FailureStage,
                Is.EqualTo(CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Recovery));
        }

        private static CharacterTestRuntimeAiLabTestCurrentChangesRequest CreateRequest(
            string operationId,
            int revision)
        {
            return new CharacterTestRuntimeAiLabTestCurrentChangesRequest(
                operationId,
                "workspace-main",
                revision,
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                "group-main",
                "entry-main",
                "card-main",
                "published.card",
                pageContext: null);
        }

        private static CharacterTestRuntimeAiLabTestCurrentChangesApplyResult Success()
        {
            return new CharacterTestRuntimeAiLabTestCurrentChangesApplyResult(
                true,
                CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.None,
                string.Empty,
                "applied",
                0,
                101L,
                101L,
                7,
                1,
                3,
                restartedFromFrameZero: true,
                contextRestored: true,
                previousSessionRecovered: false,
                publishedContentPristine: false);
        }
    }
}
