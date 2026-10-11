using System;
using System.Threading;
using MxFramework.Platform;
using MxFramework.Platform.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.PlatformUnity
{
    public sealed class UnityApplicationLifecycleTests
    {
        [Test]
        public void Reporter_StartsAtRevisionZeroWithoutSyntheticEdge()
        {
            ClaimedReporter reporter = CreateReporter(
                initialHasFocus: true,
                initialIsSuspended: false,
                capacity: 4);

            Assert.That(reporter.Current, Is.EqualTo(
                new ApplicationLifecycleSnapshot(true, false, false, 0)));
            Assert.That(reporter.TryDequeue(out _), Is.False);
        }

        [Test]
        public void Reporter_DeduplicatesAndPreservesOrthogonalOrderedFacts()
        {
            ClaimedReporter reporter = CreateReporter(true, false, 4);

            Assert.That(reporter.ReportFocus(true), Is.False);
            Assert.That(reporter.ReportSuspend(true), Is.True);
            Assert.That(reporter.ReportFocus(false), Is.True);
            Assert.That(reporter.ReportSuspend(false), Is.True);

            Assert.That(reporter.TryDequeue(out ApplicationLifecycleChange suspend), Is.True);
            Assert.That(suspend.Kind, Is.EqualTo(ApplicationLifecycleChangeKind.SuspendChanged));
            Assert.That(suspend.Snapshot.HasFocus, Is.True);
            Assert.That(suspend.Snapshot.IsSuspended, Is.True);

            Assert.That(reporter.TryDequeue(out ApplicationLifecycleChange focus), Is.True);
            Assert.That(focus.Kind, Is.EqualTo(ApplicationLifecycleChangeKind.FocusChanged));
            Assert.That(focus.Snapshot.HasFocus, Is.False);
            Assert.That(focus.Snapshot.IsSuspended, Is.True);

            Assert.That(reporter.TryDequeue(out ApplicationLifecycleChange resume), Is.True);
            Assert.That(resume.Kind, Is.EqualTo(ApplicationLifecycleChangeKind.SuspendChanged));
            Assert.That(resume.Snapshot.IsSuspended, Is.False);
        }

        [Test]
        public void Reporter_QuittingIsSticky()
        {
            ClaimedReporter reporter = CreateReporter(true, false, 2);

            Assert.That(reporter.ReportQuitting(), Is.True);
            Assert.That(reporter.ReportQuitting(), Is.False);
            Assert.That(reporter.Current.IsQuitting, Is.True);
            Assert.That(reporter.Current.Revision, Is.EqualTo(1));
        }

        [Test]
        public void Reporter_RejectsCrossThreadWritesWithoutChangingSnapshot()
        {
            ClaimedReporter reporter = CreateReporter(true, false, 2);
            bool accepted = true;
            var thread = new Thread(() => accepted = reporter.ReportSuspend(true));

            thread.Start();
            thread.Join();

            Assert.That(accepted, Is.False);
            Assert.That(reporter.Current, Is.EqualTo(
                new ApplicationLifecycleSnapshot(true, false, false, 0)));
            Assert.That(reporter.TryDequeue(out _), Is.False);
        }

        [Test]
        public void Proxy_ForwardsFactsOnlyToExplicitlyBoundReporter()
        {
            var gameObject = new GameObject("platform-lifecycle-proxy-test");
            try
            {
                UnityApplicationLifecycleProxy proxy = gameObject.AddComponent<UnityApplicationLifecycleProxy>();
                ClaimedReporter reporter = CreateReporter(true, false, 4);

                proxy.OnApplicationPause(true);
                Assert.That(reporter.Current.Revision, Is.Zero);

                proxy.Bind(reporter);
                proxy.OnApplicationFocus(false);
                proxy.OnApplicationPause(true);
                proxy.OnApplicationQuit();

                Assert.That(reporter.Current.HasFocus, Is.False);
                Assert.That(reporter.Current.IsSuspended, Is.True);
                Assert.That(reporter.Current.IsQuitting, Is.True);

                proxy.Unbind();
                proxy.OnApplicationFocus(true);
                Assert.That(reporter.Current.HasFocus, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void WarmReportAndDrain_AllocateZeroBytes()
        {
            ClaimedReporter reporter = CreateReporter(true, false, 2);
            reporter.ReportFocus(false);
            reporter.TryDequeue(out _);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20000; i++)
            {
                reporter.ReportFocus((i & 1) == 0);
                reporter.ReportSuspend((i & 2) == 0);
                while (reporter.TryDequeue(out _))
                {
                }
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        private static ClaimedReporter CreateReporter(bool initialHasFocus, bool initialIsSuspended, int capacity)
        {
            var reporter = new UnityApplicationLifecycleReporter(initialHasFocus, initialIsSuspended, capacity);
            Assert.That(reporter.TryClaimDrain(out IApplicationLifecycleDrainOwner drain), Is.True);
            return new ClaimedReporter(reporter, drain);
        }

        private sealed class ClaimedReporter : IApplicationLifecycleReporter
        {
            private readonly UnityApplicationLifecycleReporter _reporter;
            private readonly IApplicationLifecycleDrainOwner _drain;
            public ClaimedReporter(UnityApplicationLifecycleReporter reporter, IApplicationLifecycleDrainOwner drain)
            {
                _reporter = reporter;
                _drain = drain;
            }
            public ApplicationLifecycleSnapshot Current => _drain.Current;
            public bool ReportFocus(bool value) => _reporter.ReportFocus(value);
            public bool ReportSuspend(bool value) => _reporter.ReportSuspend(value);
            public bool ReportQuitting() => _reporter.ReportQuitting();
            public bool TryDequeue(out ApplicationLifecycleChange change) => _drain.TryDequeue(out change);
        }
    }
}
