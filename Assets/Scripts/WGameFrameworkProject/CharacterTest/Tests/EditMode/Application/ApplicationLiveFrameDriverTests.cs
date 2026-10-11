using System;
using System.Threading;
using System.Threading.Tasks;
using MxFramework.ApplicationStartup;
using NUnit.Framework;

namespace MxFramework.Tests.ApplicationStartup
{
    public sealed class ApplicationLiveFrameDriverTests
    {
        [Test]
        public void Publisher_GrantsOneDriverAndBootDoesNotAttemptRuntime()
        {
            var publisher = new LiveApplicationStatePublisher();

            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Assert.That(driver, Is.Not.Null);
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver duplicate), Is.False);
            Assert.That(duplicate, Is.Null);
            Assert.That(driver.Tick(0.016d).Status, Is.EqualTo(ApplicationLiveFrameTickStatus.Boot));
            Assert.That(driver.TryGetCurrentRevisionToken(out ApplicationLiveStateRevisionToken token), Is.True);
            Assert.That(token.Revision, Is.Zero);
        }

        [Test]
        public void NewRevision_SeedsOnceThenTicksFPlusOneAndFPlusTwoAndReplacementReseeds()
        {
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            var firstPump = new RecordingPumpLease("first");
            Publish(publisher, firstPump, 42);

            Assert.That(driver.Tick(0.016d).Frame, Is.EqualTo(42));
            Assert.That(driver.Tick(0.016d).Frame, Is.EqualTo(43));
            Assert.That(firstPump.TickCount, Is.EqualTo(2));
            Assert.That(firstPump.LastFrame, Is.EqualTo(43));

            var replacementPump = new RecordingPumpLease("replacement");
            Publish(publisher, replacementPump, 100);
            Assert.That(driver.Tick(0.016d).Frame, Is.EqualTo(100));
            Assert.That(driver.Tick(0.016d).Frame, Is.EqualTo(101));
            Assert.That(firstPump.TickCount, Is.EqualTo(2));
        }

        [Test]
        public void CallbackToken_RejectsDuplicateGapStaleForeignAndConcurrentDuplicate()
        {
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Publish(publisher, new RecordingPumpLease("first"), 1);
            Assert.That(driver.TryGetCurrentRevisionToken(out ApplicationLiveStateRevisionToken firstToken), Is.True);
            var callback = new CountingCallback();

            Assert.That(driver.TryDispatchCallback(firstToken, 1, callback),
                Is.EqualTo(ApplicationLiveCallbackDispatchStatus.OutOfSequence));
            Assert.That(driver.TryDispatchCallback(firstToken, 0, callback),
                Is.EqualTo(ApplicationLiveCallbackDispatchStatus.Succeeded));
            Assert.That(driver.TryDispatchCallback(firstToken, 0, callback),
                Is.EqualTo(ApplicationLiveCallbackDispatchStatus.OutOfSequence));

            Publish(publisher, new RecordingPumpLease("replacement"), 2);
            Assert.That(driver.TryDispatchCallback(firstToken, 1, callback),
                Is.EqualTo(ApplicationLiveCallbackDispatchStatus.StaleOrForeignToken));
            Assert.That(driver.TryGetCurrentRevisionToken(out ApplicationLiveStateRevisionToken currentToken), Is.True);

            var foreignPublisher = new LiveApplicationStatePublisher();
            Assert.That(foreignPublisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver foreignDriver), Is.True);
            Assert.That(foreignDriver.TryGetCurrentRevisionToken(out ApplicationLiveStateRevisionToken foreignToken), Is.True);
            Assert.That(driver.TryDispatchCallback(foreignToken, 0, callback),
                Is.EqualTo(ApplicationLiveCallbackDispatchStatus.StaleOrForeignToken));

            ApplicationLiveCallbackDispatchStatus first = default;
            ApplicationLiveCallbackDispatchStatus second = default;
            var ready = new CountdownEvent(2);
            var start = new ManualResetEventSlim(false);
            Task a = Task.Factory.StartNew(() =>
            {
                ready.Signal();
                start.Wait();
                first = driver.TryDispatchCallback(currentToken, 0, callback);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Task b = Task.Factory.StartNew(() =>
            {
                ready.Signal();
                start.Wait();
                second = driver.TryDispatchCallback(currentToken, 0, callback);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.That(ready.Wait(5000), Is.True);
            start.Set();
            Assert.That(Task.WaitAll(new[] { a, b }, 5000), Is.True);
            Assert.That(
                (first == ApplicationLiveCallbackDispatchStatus.Succeeded ? 1 : 0) +
                (second == ApplicationLiveCallbackDispatchStatus.Succeeded ? 1 : 0), Is.EqualTo(1));
            Assert.That(callback.Count, Is.EqualTo(2));
        }

        [Test]
        public void TickBoundary_PublicationFailsImmediatelyWithoutWaitingForExternalPump()
        {
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            var entered = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            var pump = new RecordingPumpLease("blocking")
            {
                OnTick = () =>
                {
                    entered.Set();
                    release.Wait();
                }
            };
            Publish(publisher, pump, 1);
            LiveApplicationState expected = publisher.Current;
            LiveApplicationState replacement = Candidate(new RecordingPumpLease("replacement"), 10);

            Task tick = Task.Factory.StartNew(() => driver.Tick(0.016d),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.That(entered.Wait(5000), Is.True);
            ApplicationStartupCommitResult publication = default;
            var publishStarted = new ManualResetEventSlim(false);
            var publishDone = new ManualResetEventSlim(false);
            Task publish = Task.Factory.StartNew(() =>
            {
                publishStarted.Set();
                publication = publisher.TryPublish(expected, replacement);
                publishDone.Set();
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.That(publishStarted.Wait(5000), Is.True);
            Assert.That(publishDone.Wait(5000), Is.True);
            Assert.That(publication.Success, Is.False);
            Assert.That(publisher.Current, Is.SameAs(expected));
            release.Set();
            Assert.That(Task.WaitAll(new[] { tick, publish }, 5000), Is.True);
            Assert.That(publisher.TryPublish(expected, replacement).Success, Is.True);
        }

        [Test]
        public void ReentrantPublicationFromTick_FailsClosedWithoutSwap()
        {
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            ApplicationStartupCommitResult nested = default;
            var pump = new RecordingPumpLease("current");
            Publish(publisher, pump, 1);
            LiveApplicationState current = publisher.Current;
            LiveApplicationState replacement = Candidate(new RecordingPumpLease("replacement"), 10);
            pump.OnTick = () => nested = publisher.TryPublish(current, replacement);

            Assert.That(driver.Tick(0.016d).Status, Is.EqualTo(ApplicationLiveFrameTickStatus.RuntimeAttemptSucceeded));
            Assert.That(nested.Success, Is.False);
            Assert.That(nested.Status, Is.EqualTo(ApplicationStartupCommitStatus.Failed));
            Assert.That(publisher.Current, Is.SameAs(current));
        }

        [Test]
        public void CallbackBoundary_ReplacementFailsImmediatelyThenOldTokenBecomesStale()
        {
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Publish(publisher, new RecordingPumpLease("current"), 1);
            LiveApplicationState expected = publisher.Current;
            Assert.That(driver.TryGetCurrentRevisionToken(out ApplicationLiveStateRevisionToken token), Is.True);
            var entered = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            var callback = new DelegatingCallback(() =>
            {
                entered.Set();
                release.Wait();
            });
            Task dispatch = Task.Factory.StartNew(
                () => driver.TryDispatchCallback(token, 0, callback),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.That(entered.Wait(5000), Is.True);
            LiveApplicationState replacement = Candidate(new RecordingPumpLease("replacement"), 10);
            var publishDone = new ManualResetEventSlim(false);
            ApplicationStartupCommitResult blocked = default;
            Task publish = Task.Factory.StartNew(() =>
            {
                blocked = publisher.TryPublish(expected, replacement);
                publishDone.Set();
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            Assert.That(publishDone.Wait(5000), Is.True);
            Assert.That(blocked.Success, Is.False);
            release.Set();
            Assert.That(Task.WaitAll(new[] { dispatch, publish }, 5000), Is.True);
            Assert.That(publisher.TryPublish(expected, replacement).Success, Is.True);
            Assert.That(driver.TryDispatchCallback(token, 1, callback),
                Is.EqualTo(ApplicationLiveCallbackDispatchStatus.StaleOrForeignToken));
        }

        [Test]
        public void AttemptObservationThrow_PoisonsGenerationPreservesRootAndReplacementReseeds()
        {
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            var pump = new RecordingPumpLease("faults")
            {
                ThrowAfterAttempt = true,
                ThrowOnObservation = true
            };
            Publish(publisher, pump, 1);

            InvalidOperationException root = Assert.Throws<InvalidOperationException>(() => driver.Tick(0.016d));
            Assert.That(root.Message, Is.EqualTo("Injected fail-fast tick failure."));
            pump.ThrowAfterAttempt = false;
            pump.ThrowOnObservation = false;
            Assert.That(driver.Tick(0.016d).Status,
                Is.EqualTo(ApplicationLiveFrameTickStatus.FrameExhausted));
            Assert.That(pump.TickCount, Is.EqualTo(1));

            var replacement = new RecordingPumpLease("replacement");
            Publish(publisher, replacement, 10);
            Assert.That(driver.Tick(0.016d).Status,
                Is.EqualTo(ApplicationLiveFrameTickStatus.RuntimeAttemptSucceeded));
            Assert.That(replacement.LastFrame, Is.EqualTo(10));
        }

        [Test]
        public void HostAttemptFailureConsumesFrameAndLongMaxExhaustsWithoutWrap()
        {
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            var pump = new RecordingPumpLease("failure") { ThrowAfterAttempt = true };
            Publish(publisher, pump, 41);

            Assert.Throws<InvalidOperationException>(() => driver.Tick(0.016d));
            pump.ThrowAfterAttempt = false;
            Assert.That(driver.Tick(0.016d).Frame, Is.EqualTo(42));

            var maxPump = new RecordingPumpLease("max");
            Publish(publisher, maxPump, long.MaxValue);
            Assert.That(driver.Tick(0.016d).Frame, Is.EqualTo(long.MaxValue));
            Assert.That(driver.Tick(0.016d).Status,
                Is.EqualTo(ApplicationLiveFrameTickStatus.FrameExhausted));
            Assert.That(maxPump.TickCount, Is.EqualTo(1));
        }

        [Test]
        public void FailureBeforeHostAttempt_DoesNotConsumeFrame()
        {
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            var pump = new RecordingPumpLease("no-attempt") { SuppressAttempt = true };
            Publish(publisher, pump, 7);

            Assert.That(driver.Tick(0.016d).Status,
                Is.EqualTo(ApplicationLiveFrameTickStatus.NoRuntimeAttempt));
            pump.SuppressAttempt = false;
            Assert.That(driver.Tick(0.016d).Frame, Is.EqualTo(7));
        }

        [Test]
        public void WarmedTickAndTokenReads_AllocateZeroBytes()
        {
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Publish(publisher, new RecordingPumpLease("noalloc"), 0);
            driver.Tick(0.016d);
            driver.TryGetCurrentRevisionToken(out _);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                driver.Tick(0.016d);
                driver.TryGetCurrentRevisionToken(out _);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(after - before, Is.Zero);
        }

        private static void Publish(
            LiveApplicationStatePublisher publisher,
            RecordingPumpLease pump,
            long nextFrame)
        {
            ApplicationStartupCommitResult result =
                publisher.TryPublish(publisher.Current, Candidate(pump, nextFrame));
            Assert.That(result.Success, Is.True, result.Error.Message);
        }

        private static LiveApplicationState Candidate(RecordingPumpLease pump, long nextFrame) =>
            LiveApplicationState.CreateCandidate(true, nextFrame, pump);

        private sealed class CountingCallback : IApplicationLiveRevisionCallback
        {
            private int _count;
            public int Count => Volatile.Read(ref _count);
            public void Invoke() => Interlocked.Increment(ref _count);
        }

        private sealed class DelegatingCallback : IApplicationLiveRevisionCallback
        {
            private readonly Action _action;
            public DelegatingCallback(Action action) => _action = action;
            public void Invoke() => _action();
        }

        private sealed class RecordingPumpLease :
            IApplicationLiveSessionLease,
            IApplicationLiveRuntimePump
        {
            private long _lastAttemptFrame = -1;

            public RecordingPumpLease(string sessionId) => SessionId = sessionId;
            public string SessionId { get; }
            public int TickCount { get; private set; }
            public long LastFrame { get; private set; } = -1;
            public bool SuppressAttempt { get; set; }
            public bool ThrowAfterAttempt { get; set; }
            public bool ThrowOnObservation { get; set; }
            public Action OnTick { get; set; }

            public void Tick(long frameIndex, double deltaTime, double elapsedTime)
            {
                if (SuppressAttempt) return;
                _lastAttemptFrame = frameIndex;
                LastFrame = frameIndex;
                TickCount++;
                OnTick?.Invoke();
                if (ThrowAfterAttempt)
                    throw new InvalidOperationException("Injected fail-fast tick failure.");
            }

            public ApplicationLiveRuntimeAttemptStatus GetTickAttemptStatus(long frameIndex) =>
                ThrowOnObservation
                    ? throw new InvalidOperationException("Injected attempt observation failure.")
                    : _lastAttemptFrame == frameIndex
                    ? ApplicationLiveRuntimeAttemptStatus.Succeeded
                    : ApplicationLiveRuntimeAttemptStatus.None;
            public void Dispose() { }
        }
    }
}
