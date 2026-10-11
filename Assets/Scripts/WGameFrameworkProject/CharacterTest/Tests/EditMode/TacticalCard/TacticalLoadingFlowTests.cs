using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MxFramework.Game.CharacterTest.TacticalCard;
using NUnit.Framework;

namespace MxFramework.Tests.EditMode.CharacterTest
{
    public sealed class TacticalLoadingFlowTests
    {
        private sealed class Candidate : IDisposable
        {
            internal int DisposeCount;
            public void Dispose() => DisposeCount++;
        }

        [Test] public void BeginPublishesOnlyTheActualStage()
        {
            var flow = new TacticalLoadingFlow();
            Assert.That(flow.TryBegin(TacticalLoadingOperation.Continue, TacticalLoadingStage.OpeningProgress, out long id), Is.True);
            Assert.That(flow.Visible && flow.Running && flow.IsCurrent(id), Is.True);
            Assert.That(flow.Stage, Is.EqualTo(TacticalLoadingStage.OpeningProgress));
            Assert.That(flow.Issue, Is.Empty);
        }

        [Test] public void DuplicateBeginCannotReplaceRunningAttempt()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.Deploy, TacticalLoadingStage.SavingProgress, out long id);
            int revision = flow.Revision;
            Assert.That(flow.TryBegin(TacticalLoadingOperation.NewGame, TacticalLoadingStage.OpeningProgress, out long duplicate), Is.False);
            Assert.That(duplicate, Is.EqualTo(id));
            Assert.That(flow.Operation, Is.EqualTo(TacticalLoadingOperation.Deploy));
            Assert.That(flow.Revision, Is.EqualTo(revision));
        }

        [Test] public void MeasuredUnitsAreStageFactsAndClearOnStageChange()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.Deploy, TacticalLoadingStage.InstallingBattle, out long id);
            Assert.That(flow.TotalUnits, Is.Zero);
            Assert.That(flow.ReportUnits(id, 1, 3), Is.True);
            Assert.That(flow.CompletedUnits, Is.EqualTo(1));
            Assert.That(flow.TotalUnits, Is.EqualTo(3));
            Assert.That(flow.ReportUnits(id, 4, 3), Is.False);
            flow.Report(id, TacticalLoadingStage.PublishingSession);
            Assert.That(flow.TotalUnits, Is.Zero);
        }

        [Test] public void LateUnitsCannotPolluteANewAttempt()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.Deploy, TacticalLoadingStage.InstallingBattle, out long old);
            flow.Interrupt(); flow.TryBegin(TacticalLoadingOperation.Continue, TacticalLoadingStage.OpeningProgress, out long current);
            Assert.That(flow.ReportUnits(old, 2, 2), Is.False);
            Assert.That(flow.TotalUnits, Is.Zero);
            Assert.That(flow.IsCurrent(current), Is.True);
        }

        [Test] public void CompletionHasNoMinimumWait()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.Continue, TacticalLoadingStage.OpeningProgress, out long id);
            Assert.That(flow.Complete(id), Is.True);
            Assert.That(flow.Visible, Is.False);
        }

        [Test] public void FailureRetainsTheFailedStageAndSupportsExplicitReturn()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.NewGame, TacticalLoadingStage.SavingProgress, out long id);
            flow.Fail(id, "disk full", true);
            Assert.That(flow.Status, Is.EqualTo(TacticalLoadingStatus.Failed));
            Assert.That(flow.Stage, Is.EqualTo(TacticalLoadingStage.SavingProgress));
            Assert.That(flow.Issue, Is.EqualTo("disk full"));
            Assert.That(flow.CanRetry, Is.True);
            Assert.That(flow.Complete(id), Is.False);
            Assert.That(flow.DismissFailure(), Is.True);
            Assert.That(flow.Visible, Is.False);
        }

        [Test] public void RunningCannotBeDismissedAsAFailure()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.Return, TacticalLoadingStage.PreparingSession, out long id);
            Assert.That(flow.DismissFailure(), Is.False);
            Assert.That(flow.IsCurrent(id), Is.True);
        }

        [Test] public void NonRetryableFailureStaysVisibleUntilReturned()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.NewGame, TacticalLoadingStage.PublishingSession, out long id);
            flow.Fail(id, "saved without publication", false);
            Assert.That(flow.CanRetry, Is.False);
            Assert.That(flow.Visible, Is.True);
        }

        [Test] public void RetryStartsANewGenerationAndClearsPriorIssue()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.Profile, TacticalLoadingStage.OpeningProgress, out long first);
            flow.Fail(first, "missing", true);
            flow.TryBegin(TacticalLoadingOperation.Profile, TacticalLoadingStage.OpeningProgress, out long second);
            Assert.That(second, Is.GreaterThan(first));
            Assert.That(flow.Issue, Is.Empty);
            Assert.That(flow.Fail(first, "late failure", true), Is.False);
            Assert.That(flow.IsCurrent(second), Is.True);
        }

        [Test] public void InterruptInvalidatesEveryLateMutation()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.Deploy, TacticalLoadingStage.SavingProgress, out long id);
            flow.Interrupt();
            Assert.That(flow.Visible, Is.False);
            Assert.That(flow.Report(id, TacticalLoadingStage.InstallingBattle), Is.False);
            Assert.That(flow.Fail(id, "late", true), Is.False);
            Assert.That(flow.Complete(id), Is.False);
        }

        [Test] public void RepeatedEntryAndCompletionLeaveNoActiveAttempt()
        {
            var flow = new TacticalLoadingFlow();
            for (int i = 0; i < 100; i++)
            {
                Assert.That(flow.TryBegin(TacticalLoadingOperation.Continue, TacticalLoadingStage.OpeningProgress, out long id), Is.True);
                flow.Report(id, TacticalLoadingStage.PublishingSession);
                flow.Complete(id);
                Assert.That(flow.Visible, Is.False);
            }
        }

        [Test] public async Task ReplacementReportsStagesAtActualCallbacks()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.Continue, TacticalLoadingStage.OpeningProgress, out long id);
            var stages = new List<TacticalLoadingStage>();
            var candidate = new Candidate();
            var result = await flow.ReplaceAsync(id,
                () => { stages.Add(flow.Stage); return candidate; },
                () => { stages.Add(flow.Stage); return Task.FromResult(true); },
                _ => stages.Add(flow.Stage), () => true);
            Assert.That(result, Is.EqualTo(TacticalCheckpointTransition.Published));
            Assert.That(stages, Is.EqualTo(new[] { TacticalLoadingStage.PreparingSession, TacticalLoadingStage.SavingProgress, TacticalLoadingStage.PublishingSession }));
            Assert.That(candidate.DisposeCount, Is.EqualTo(1));
        }

        [Test] public async Task RejectedSaveDisposesCandidateAndDoesNotPublish()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.NewGame, TacticalLoadingStage.OpeningProgress, out long id);
            var candidate = new Candidate(); int published = 0;
            var result = await flow.ReplaceAsync(id, () => candidate, () => Task.FromResult(false), _ => published++, () => true);
            Assert.That(result, Is.EqualTo(TacticalCheckpointTransition.SaveRejected));
            Assert.That(published, Is.Zero);
            Assert.That(candidate.DisposeCount, Is.EqualTo(1));
            Assert.That(flow.Stage, Is.EqualTo(TacticalLoadingStage.SavingProgress));
        }

        [Test] public async Task InterruptedBeforePrepareDoesNoWork()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.Return, TacticalLoadingStage.PreparingSession, out long id);
            flow.Interrupt(); int calls = 0;
            var result = await flow.ReplaceAsync(id, () => { calls++; return new Candidate(); }, () => { calls++; return Task.FromResult(true); }, _ => calls++, () => true);
            Assert.That(result, Is.EqualTo(TacticalCheckpointTransition.Superseded));
            Assert.That(calls, Is.Zero);
        }

        [Test] public async Task InvalidatedAfterPrepareSkipsCommitAndPublication()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.Return, TacticalLoadingStage.PreparingSession, out long id);
            var candidate = new Candidate(); int calls = 0;
            var result = await flow.ReplaceAsync(id, () => { flow.Interrupt(); return candidate; }, () => { calls++; return Task.FromResult(true); }, _ => calls++, () => true);
            Assert.That(result, Is.EqualTo(TacticalCheckpointTransition.Superseded));
            Assert.That(calls, Is.Zero);
            Assert.That(candidate.DisposeCount, Is.EqualTo(1));
        }

        [Test] public async Task InterruptedDuringSaveNeverPublishesALateSession()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.NewGame, TacticalLoadingStage.OpeningProgress, out long id);
            var saved = new TaskCompletionSource<bool>(); var candidate = new Candidate(); int published = 0;
            Task<TacticalCheckpointTransition> pending = flow.ReplaceAsync(id, () => candidate, () => saved.Task, _ => published++, () => true);
            flow.Interrupt(); saved.SetResult(true);
            Assert.That(await pending, Is.EqualTo(TacticalCheckpointTransition.SavedWithoutPublication));
            Assert.That(published, Is.Zero);
            Assert.That(candidate.DisposeCount, Is.EqualTo(1));
            Assert.That(flow.Visible, Is.False);
        }

        [Test] public async Task OldSaveCompletionCannotOverwriteANewAttempt()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.NewGame, TacticalLoadingStage.OpeningProgress, out long old);
            var saved = new TaskCompletionSource<bool>();
            Task<TacticalCheckpointTransition> pending = flow.ReplaceAsync(old, () => new Candidate(), () => saved.Task, _ => Assert.Fail("late publication"), () => true);
            flow.Interrupt(); flow.TryBegin(TacticalLoadingOperation.Profile, TacticalLoadingStage.OpeningProgress, out long current);
            saved.SetResult(true); await pending;
            Assert.That(flow.Complete(old), Is.False);
            Assert.That(flow.IsCurrent(current), Is.True);
            Assert.That(flow.Stage, Is.EqualTo(TacticalLoadingStage.OpeningProgress));
        }

        [Test] public async Task ChangedContextDuringSavePreservesCurrentSession()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.Profile, TacticalLoadingStage.OpeningProgress, out long id);
            var saved = new TaskCompletionSource<bool>(); bool current = true; int published = 0;
            Task<TacticalCheckpointTransition> pending = flow.ReplaceAsync(id, () => new Candidate(), () => saved.Task, _ => published++, () => current);
            current = false; saved.SetResult(true);
            Assert.That(await pending, Is.EqualTo(TacticalCheckpointTransition.SavedWithoutPublication));
            Assert.That(published, Is.Zero);
        }

        [Test] public async Task DuplicateInputDuringSaveCannotStartAnotherTransaction()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.NewGame, TacticalLoadingStage.OpeningProgress, out long id);
            var saved = new TaskCompletionSource<bool>(); int commits = 0;
            var pending = flow.ReplaceAsync(id, () => new Candidate(), () => { commits++; return saved.Task; }, _ => { }, () => true);
            Assert.That(flow.TryBegin(TacticalLoadingOperation.NewGame, TacticalLoadingStage.OpeningProgress, out _), Is.False);
            Assert.That(commits, Is.EqualTo(1)); saved.SetResult(true); await pending;
        }

        [Test] public void CommitExceptionDisposesPreparedCandidate()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.Return, TacticalLoadingStage.PreparingSession, out long id);
            var candidate = new Candidate();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await flow.ReplaceAsync(id, () => candidate,
                () => Task.FromException<bool>(new InvalidOperationException("save failed")), _ => Assert.Fail("published"), () => true));
            Assert.That(candidate.DisposeCount, Is.EqualTo(1));
        }

        [Test] public void PublicationExceptionIsDistinguishedFromUncommittedFailure()
        {
            var flow = new TacticalLoadingFlow();
            flow.TryBegin(TacticalLoadingOperation.NewGame, TacticalLoadingStage.OpeningProgress, out long id);
            var candidate = new Candidate(); int committed = 0;
            Assert.ThrowsAsync<TacticalCheckpointPublicationException>(async () => await flow.ReplaceAsync(id, () => candidate,
                () => { committed++; return Task.FromResult(true); }, _ => throw new InvalidOperationException("bind failed"), () => true));
            Assert.That(committed, Is.EqualTo(1));
            Assert.That(flow.Stage, Is.EqualTo(TacticalLoadingStage.PublishingSession));
            Assert.That(candidate.DisposeCount, Is.EqualTo(1));
        }
    }
}
