using System;
using MxFramework.ApplicationStartup;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.ApplicationStartup
{
    public sealed class ApplicationLiveRetirementTests
    {
        [Test]
        public void DisposableLease_SucceedsExactlyOnce_ThenAllowsReservationRelease()
        {
            var lease = new RecordingLease("sync");
            var driver = new ApplicationLiveRetirementDriver(4, lease.SessionId, lease);

            driver.Begin();
            Assert.AreEqual(ApplicationLiveRetirementStatus.Running, driver.Status);
            Assert.IsFalse(driver.CanReleaseReservation);

            driver.Tick();
            driver.Tick();

            Assert.AreEqual(ApplicationLiveRetirementStatus.Succeeded, driver.Status);
            Assert.IsTrue(driver.CanReleaseReservation);
            Assert.AreEqual(1, lease.DisposeCount);
            Assert.AreEqual(0, driver.WarningCount);
            Assert.IsTrue(driver.OwnershipDebt.IsNone);
        }

        [Test]
        public void AsynchronousLease_DoesNotReleaseReservationBeforeTruthfulSuccess()
        {
            var operation = new ScriptedOperation(RuntimeOperationStatus.Succeeded, 3);
            var lease = new RetirementSourceLease("async", operation);
            var driver = new ApplicationLiveRetirementDriver(7, lease.SessionId, lease);

            driver.Begin();
            driver.Tick();
            driver.Tick();

            Assert.AreEqual(ApplicationLiveRetirementStatus.Running, driver.Status);
            Assert.IsFalse(driver.CanReleaseReservation);

            driver.Tick();

            Assert.AreEqual(ApplicationLiveRetirementStatus.Succeeded, driver.Status);
            Assert.IsTrue(driver.CanReleaseReservation);
            Assert.AreEqual(3, operation.TickCount);
            Assert.AreEqual(1, lease.BeginCount);
            Assert.AreEqual(0, lease.DisposeCount);
        }

        [Test]
        public void FailedOperation_KeepsReservationPoisoned_AndPreservesOrderedWarnings()
        {
            var warnings = new[]
            {
                new ApplicationLiveRetirementWarning("scene", "unload.failed", RuntimeOperationError.None, "Scene unload failed.", null),
                new ApplicationLiveRetirementWarning("runtime", "dispose.failed", RuntimeOperationError.None, "Runtime dispose failed.", null)
            };
            var debt = new ApplicationLiveRetirementOwnershipDebt("scene", "owner.remaining", "Scene ownership remains.", null);
            var operation = new ScriptedOperation(RuntimeOperationStatus.Failed, 1, warnings, debt);
            var lease = new RetirementSourceLease("failed", operation);
            var driver = new ApplicationLiveRetirementDriver(9, lease.SessionId, lease);

            driver.Begin();
            driver.Tick();
            bool reservationReleased = driver.CanReleaseReservation;

            Assert.AreEqual(ApplicationLiveRetirementStatus.Failed, driver.Status);
            Assert.IsFalse(reservationReleased);
            Assert.AreEqual(0, lease.DisposeCount, "The failed asynchronous operation remains the retirement owner.");
            Assert.AreEqual(2, driver.WarningCount);
            Assert.AreEqual("unload.failed", driver.GetWarning(0).StableCode);
            Assert.AreEqual("dispose.failed", driver.GetWarning(1).StableCode);
            Assert.AreEqual("owner.remaining", driver.OwnershipDebt.StableCode);
        }

        [Test]
        public void DisposeException_BecomesOwnershipDebt_AndIsNotRetried()
        {
            var expected = new InvalidOperationException("dispose failed");
            var lease = new RecordingLease("throwing", expected);
            var driver = new ApplicationLiveRetirementDriver(10, lease.SessionId, lease);

            driver.Begin();
            driver.Tick();
            driver.Tick();

            Assert.AreEqual(ApplicationLiveRetirementStatus.Failed, driver.Status);
            Assert.IsFalse(driver.CanReleaseReservation);
            Assert.AreEqual(1, lease.DisposeCount);
            Assert.AreEqual(1, driver.WarningCount);
            Assert.AreEqual("lease.dispose-exception", driver.GetWarning(0).StableCode);
            Assert.AreSame(expected, driver.GetWarning(0).Exception);
            Assert.AreEqual("lease.dispose-exception", driver.OwnershipDebt.StableCode);
        }

        [Test]
        public void TickException_IsTruthfulTerminalDebt_AndDoesNotTickAgain()
        {
            var expected = new InvalidOperationException("tick failed");
            var operation = new ScriptedOperation(RuntimeOperationStatus.Succeeded, 2, throwOnTick: expected);
            var lease = new RetirementSourceLease("tick-throw", operation);
            var driver = new ApplicationLiveRetirementDriver(11, lease.SessionId, lease);

            driver.Begin();
            driver.Tick();
            driver.Tick();

            Assert.AreEqual(ApplicationLiveRetirementStatus.Failed, driver.Status);
            Assert.IsFalse(driver.CanReleaseReservation);
            Assert.AreEqual(1, operation.TickCount);
            Assert.AreEqual("operation.tick-exception", driver.GetWarning(0).StableCode);
            Assert.AreSame(expected, driver.OwnershipDebt.Exception);
        }

        [Test]
        public void SourceBeginException_IsTruthfulTerminalDebt()
        {
            var expected = new InvalidOperationException("begin failed");
            var lease = new RetirementSourceLease("begin-throw", null, expected);
            var driver = new ApplicationLiveRetirementDriver(12, lease.SessionId, lease);

            driver.Begin();
            driver.Begin();

            Assert.AreEqual(ApplicationLiveRetirementStatus.Failed, driver.Status);
            Assert.IsFalse(driver.CanReleaseReservation);
            Assert.AreEqual(1, lease.BeginCount);
            Assert.AreEqual("begin.exception", driver.OwnershipDebt.StableCode);
            Assert.AreSame(expected, driver.GetWarning(0).Exception);
        }

        [Test]
        public void SuccessWithOwnershipDebt_FailsClosed()
        {
            var debt = new ApplicationLiveRetirementOwnershipDebt("scene", "scene.owner.remaining", "Scene owner remains.", null);
            var operation = new ScriptedOperation(RuntimeOperationStatus.Succeeded, 1, null, debt);
            var lease = new RetirementSourceLease("contradictory", operation);
            var driver = new ApplicationLiveRetirementDriver(13, lease.SessionId, lease);

            driver.Begin();
            driver.Tick();

            Assert.AreEqual(ApplicationLiveRetirementStatus.Failed, driver.Status);
            Assert.IsFalse(driver.CanReleaseReservation);
            Assert.AreEqual("scene.owner.remaining", driver.OwnershipDebt.StableCode);
            Assert.AreEqual("operation.success-with-debt", driver.GetWarning(0).StableCode);
        }

        [Test]
        public void ContradictorySucceededError_FailsClosedWithLocalDebt()
        {
            var operation = new ContradictoryOperation
            {
                Status = RuntimeOperationStatus.Succeeded,
                Progress = 1f,
                Error = new RuntimeOperationError("test.unexpected", "Success cannot retain an error.")
            };
            var lease = new RetirementSourceLease("invalid-success", operation);
            var driver = new ApplicationLiveRetirementDriver(15, lease.SessionId, lease);

            driver.Begin();

            Assert.AreEqual(ApplicationLiveRetirementStatus.Failed, driver.Status);
            Assert.IsFalse(driver.CanReleaseReservation);
            Assert.AreEqual(0, operation.TickCount);
            Assert.AreEqual("operation.invalid-error", driver.GetWarning(0).StableCode);
            Assert.AreEqual("operation.invalid-error", driver.OwnershipDebt.StableCode);
        }

        [Test]
        public void InvalidProgressValues_FailClosedWithoutTickingOperation()
        {
            float[] invalidProgress = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0.01f, 1.01f };
            for (int i = 0; i < invalidProgress.Length; i++)
            {
                var operation = new ContradictoryOperation
                {
                    Status = RuntimeOperationStatus.Running,
                    Progress = invalidProgress[i],
                    Error = RuntimeOperationError.None
                };
                var lease = new RetirementSourceLease("invalid-progress", operation);
                var driver = new ApplicationLiveRetirementDriver(16 + i, lease.SessionId, lease);

                driver.Begin();

                Assert.AreEqual(ApplicationLiveRetirementStatus.Failed, driver.Status);
                Assert.AreEqual(0, operation.TickCount);
                Assert.AreEqual("operation.invalid-progress", driver.GetWarning(0).StableCode);
                Assert.AreEqual("operation.invalid-progress", driver.OwnershipDebt.StableCode);
            }
        }

        [Test]
        public void ContradictoryRunningError_IsRejectedBeforeNextTick()
        {
            var operation = new ContradictoryOperation
            {
                Status = RuntimeOperationStatus.Running,
                Progress = 0.5f,
                Error = RuntimeOperationError.None
            };
            var lease = new RetirementSourceLease("invalid-running", operation);
            var driver = new ApplicationLiveRetirementDriver(22, lease.SessionId, lease);
            driver.Begin();
            operation.Error = new RuntimeOperationError("test.unexpected", "Running cannot expose an error.");

            driver.Tick();

            Assert.AreEqual(ApplicationLiveRetirementStatus.Failed, driver.Status);
            Assert.AreEqual(0, operation.TickCount);
            Assert.AreEqual("operation.invalid-error", driver.GetWarning(0).StableCode);
        }

        [Test]
        public void ContradictoryTerminalTuples_FailClosedDuringBegin()
        {
            var succeededWithoutCompleteProgress = new ContradictoryOperation
            {
                Status = RuntimeOperationStatus.Succeeded,
                Progress = 0.5f,
                Error = RuntimeOperationError.None
            };
            var failedWithoutError = new ContradictoryOperation
            {
                Status = RuntimeOperationStatus.Failed,
                Progress = 0.5f,
                Error = RuntimeOperationError.None
            };
            var cancelledWithError = new ContradictoryOperation
            {
                Status = RuntimeOperationStatus.Cancelled,
                Progress = 0.5f,
                Error = new RuntimeOperationError("test.unexpected", "Cancelled cannot expose an error.")
            };

            AssertInvalidTerminalTuple(succeededWithoutCompleteProgress, "operation.invalid-progress");
            AssertInvalidTerminalTuple(failedWithoutError, "operation.invalid-error");
            AssertInvalidTerminalTuple(cancelledWithError, "operation.invalid-error");
        }

        [Test]
        public void RunningToPendingStatusRegression_FailsClosedBeforeNextTick()
        {
            var operation = new ContradictoryOperation
            {
                Status = RuntimeOperationStatus.Running,
                Progress = 0.5f,
                Error = RuntimeOperationError.None
            };
            var lease = new RetirementSourceLease("status-regression", operation);
            var driver = new ApplicationLiveRetirementDriver(24, lease.SessionId, lease);
            driver.Begin();
            operation.Status = RuntimeOperationStatus.Pending;

            driver.Tick();

            Assert.AreEqual(ApplicationLiveRetirementStatus.Failed, driver.Status);
            Assert.AreEqual(0, operation.TickCount);
            Assert.AreEqual("operation.invalid-status", driver.GetWarning(0).StableCode);
            Assert.AreEqual("operation.invalid-status", driver.OwnershipDebt.StableCode);
        }

        [Test]
        public void PublisherReservation_ReleasesOnlyAfterAsynchronousRetirementSucceeds()
        {
            var publisher = new LiveApplicationStatePublisher(1);
            var operation = new ScriptedOperation(RuntimeOperationStatus.Succeeded, 2);
            var retiringLease = new RetirementSourceLease("retiring", operation);
            Assert.IsTrue(publisher.TryPublish(
                publisher.Current,
                LiveApplicationState.CreateCandidate(true, 1, retiringLease)).Success);
            Assert.IsTrue(publisher.TryDequeueRetired(out LiveApplicationStateRetirement bootRetirement));
            bootRetirement.Dispose();
            Assert.AreEqual(0, publisher.RetiredCount);

            Assert.IsTrue(publisher.TryPublish(
                publisher.Current,
                LiveApplicationState.CreateCandidate(true, 2, new RecordingLease("current"))).Success);
            Assert.IsTrue(publisher.TryDequeueRetired(out LiveApplicationStateRetirement retirement));
            retirement.BeginRetirement();
            retirement.TickRetirement();

            LiveApplicationState waitingCandidate = LiveApplicationState.CreateCandidate(
                true,
                3,
                new RecordingLease("waiting"));
            ApplicationStartupCommitResult full = publisher.TryPublish(publisher.Current, waitingCandidate);
            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, full.Status);
            Assert.AreEqual(ApplicationStartupErrorCode.CleanupIncomplete, full.Error.Code);
            Assert.AreEqual(ApplicationLiveRetirementStatus.Running, retirement.RetirementStatus);

            retirement.TickRetirement();

            Assert.AreEqual(ApplicationLiveRetirementStatus.Succeeded, retirement.RetirementStatus);
            Assert.AreEqual(0, publisher.RetiredCount);
            Assert.IsTrue(publisher.TryPublish(publisher.Current, waitingCandidate).Success);
        }

        [Test]
        public void PublisherReservation_RemainsPoisonedAfterRetirementDebt()
        {
            var publisher = new LiveApplicationStatePublisher(1);
            var debt = new ApplicationLiveRetirementOwnershipDebt(
                "scene",
                "scene.owner.remaining",
                "Scene ownership remains.",
                null);
            var warning = new ApplicationLiveRetirementWarning(
                "scene",
                "scene.unload.failed",
                RuntimeOperationError.None,
                "Scene unload failed.",
                null);
            var operation = new ScriptedOperation(
                RuntimeOperationStatus.Failed,
                1,
                new[] { warning },
                debt);
            Assert.IsTrue(publisher.TryPublish(
                publisher.Current,
                LiveApplicationState.CreateCandidate(
                    true,
                    1,
                    new RetirementSourceLease("retiring", operation))).Success);
            Assert.IsTrue(publisher.TryDequeueRetired(out LiveApplicationStateRetirement bootRetirement));
            bootRetirement.Dispose();
            Assert.IsTrue(publisher.TryPublish(
                publisher.Current,
                LiveApplicationState.CreateCandidate(true, 2, new RecordingLease("current"))).Success);
            LiveApplicationState published = publisher.Current;
            Assert.IsTrue(publisher.TryDequeueRetired(out LiveApplicationStateRetirement retirement));

            retirement.BeginRetirement();
            retirement.TickRetirement();

            Assert.AreEqual(ApplicationLiveRetirementStatus.Failed, retirement.RetirementStatus);
            Assert.AreEqual("scene.unload.failed", retirement.GetWarning(0).StableCode);
            Assert.AreEqual("scene.owner.remaining", retirement.OwnershipDebt.StableCode);
            Assert.AreEqual(1, publisher.RetiredCount);
            ApplicationStartupCommitResult blocked = publisher.TryPublish(
                published,
                LiveApplicationState.CreateCandidate(true, 3, new RecordingLease("blocked")));
            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, blocked.Status);
            Assert.AreSame(published, publisher.Current);
        }

        [Test]
        public void WarmedTerminalPollAndTick_AllocateZeroBytes()
        {
            var lease = new RecordingLease("noalloc");
            var driver = new ApplicationLiveRetirementDriver(14, lease.SessionId, lease);
            driver.Begin();
            driver.Tick();

            _ = driver.Status;
            _ = driver.Progress;
            _ = driver.Error;
            _ = driver.WarningCount;
            _ = driver.OwnershipDebt;
            _ = driver.CanReleaseReservation;
            driver.Tick();
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                driver.Tick();
                _ = driver.Status;
                _ = driver.Progress;
                _ = driver.Error;
                _ = driver.WarningCount;
                _ = driver.OwnershipDebt;
                _ = driver.CanReleaseReservation;
            }

            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        private sealed class RecordingLease : IApplicationLiveSessionLease, IApplicationLiveRuntimePump
        {
            private readonly Exception _disposeException;

            public RecordingLease(string sessionId, Exception disposeException = null)
            {
                SessionId = sessionId;
                _disposeException = disposeException;
            }

            public string SessionId { get; }
            public int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
                if (_disposeException != null)
                {
                    throw _disposeException;
                }
            }
            public void Tick(long frameIndex, double deltaTime, double elapsedTime) { }
            public ApplicationLiveRuntimeAttemptStatus GetTickAttemptStatus(long frameIndex) =>
                ApplicationLiveRuntimeAttemptStatus.None;
        }

        private static void AssertInvalidTerminalTuple(ContradictoryOperation operation, string expectedCode)
        {
            var lease = new RetirementSourceLease("invalid-terminal", operation);
            var driver = new ApplicationLiveRetirementDriver(23, lease.SessionId, lease);

            driver.Begin();

            Assert.AreEqual(ApplicationLiveRetirementStatus.Failed, driver.Status);
            Assert.IsFalse(driver.CanReleaseReservation);
            Assert.AreEqual(0, operation.TickCount);
            Assert.AreEqual(expectedCode, driver.GetWarning(0).StableCode);
            Assert.AreEqual(expectedCode, driver.OwnershipDebt.StableCode);
        }

        private sealed class RetirementSourceLease : IApplicationLiveSessionLease, IApplicationLiveRetirementSource, IApplicationLiveRuntimePump
        {
            private readonly IApplicationLiveRetirementOperation _operation;
            private readonly Exception _beginException;

            public RetirementSourceLease(
                string sessionId,
                IApplicationLiveRetirementOperation operation,
                Exception beginException = null)
            {
                SessionId = sessionId;
                _operation = operation;
                _beginException = beginException;
            }

            public string SessionId { get; }
            public int BeginCount { get; private set; }
            public int DisposeCount { get; private set; }

            public IApplicationLiveRetirementOperation BeginRetirement(in ApplicationLiveRetirementContext context)
            {
                BeginCount++;
                if (_beginException != null)
                {
                    throw _beginException;
                }

                return _operation;
            }

            public void Dispose() => DisposeCount++;
            public void Tick(long frameIndex, double deltaTime, double elapsedTime) { }
            public ApplicationLiveRuntimeAttemptStatus GetTickAttemptStatus(long frameIndex) =>
                ApplicationLiveRuntimeAttemptStatus.None;
        }

        private sealed class ScriptedOperation : IApplicationLiveRetirementOperation
        {
            private readonly RuntimeOperationStatus _terminalStatus;
            private readonly int _ticksToTerminal;
            private readonly ApplicationLiveRetirementWarning[] _warnings;
            private readonly ApplicationLiveRetirementOwnershipDebt _debt;
            private readonly Exception _throwOnTick;
            private RuntimeOperationStatus _status = RuntimeOperationStatus.Running;

            public ScriptedOperation(
                RuntimeOperationStatus terminalStatus,
                int ticksToTerminal,
                ApplicationLiveRetirementWarning[] warnings = null,
                ApplicationLiveRetirementOwnershipDebt debt = default,
                Exception throwOnTick = null)
            {
                _terminalStatus = terminalStatus;
                _ticksToTerminal = ticksToTerminal;
                _warnings = warnings ?? Array.Empty<ApplicationLiveRetirementWarning>();
                _debt = debt;
                _throwOnTick = throwOnTick;
            }

            public string OperationId => "test.retirement";
            public RuntimeOperationStatus Status => _status;
            public float Progress => _status == RuntimeOperationStatus.Succeeded ? 1f : 0.5f;
            public RuntimeOperationError Error => _status == RuntimeOperationStatus.Failed
                ? new RuntimeOperationError("test.failed", "Test retirement failed.")
                : RuntimeOperationError.None;
            public int WarningCount => _warnings.Length;
            public ApplicationLiveRetirementOwnershipDebt OwnershipDebt => _debt;
            public int TickCount { get; private set; }

            public ApplicationLiveRetirementWarning GetWarning(int index) => _warnings[index];

            public void Tick(in ApplicationLiveRetirementContext context)
            {
                TickCount++;
                if (_throwOnTick != null)
                {
                    throw _throwOnTick;
                }

                if (TickCount >= _ticksToTerminal)
                {
                    _status = _terminalStatus;
                }
            }
        }

        private sealed class ContradictoryOperation : IApplicationLiveRetirementOperation
        {
            public string OperationId => "test.contradictory-retirement";
            public RuntimeOperationStatus Status { get; set; }
            public float Progress { get; set; }
            public RuntimeOperationError Error { get; set; }
            public int WarningCount => 0;
            public ApplicationLiveRetirementOwnershipDebt OwnershipDebt => default;
            public int TickCount { get; private set; }

            public ApplicationLiveRetirementWarning GetWarning(int index) =>
                throw new ArgumentOutOfRangeException(nameof(index));

            public void Tick(in ApplicationLiveRetirementContext context) => TickCount++;
        }
    }
}
