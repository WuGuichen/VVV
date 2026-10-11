using System;
using System.Reflection;
using MxFramework.Platform;
using NUnit.Framework;

namespace MxFramework.Tests.Platform
{
    public sealed class ApplicationLifecycleBufferTests
    {
        [Test]
        public void DuplicateFocus_IsDeduplicatedAndRevisionAdvancesOnce()
        {
            ClaimedBuffer buffer = CreateBuffer(capacity: 4);

            Assert.IsFalse(buffer.ReportFocus(true));
            Assert.IsTrue(buffer.ReportFocus(false));
            Assert.IsFalse(buffer.ReportFocus(false));

            Assert.AreEqual(1L, buffer.Current.Revision);
            Assert.IsFalse(buffer.Current.HasFocus);
            Assert.IsTrue(buffer.TryDequeue(out ApplicationLifecycleChange change));
            Assert.AreEqual(ApplicationLifecycleChangeKind.FocusChanged, change.Kind);
            Assert.AreEqual(1L, change.Snapshot.Revision);
            Assert.IsFalse(buffer.TryDequeue(out _));
        }

        [Test]
        public void FocusAndSuspend_AreOrthogonalAndKeepReportOrder()
        {
            ClaimedBuffer buffer = CreateBuffer(capacity: 4);

            Assert.IsTrue(buffer.ReportSuspend(true));
            Assert.IsTrue(buffer.ReportFocus(false));

            Assert.IsTrue(buffer.TryDequeue(out ApplicationLifecycleChange suspend));
            Assert.IsTrue(buffer.TryDequeue(out ApplicationLifecycleChange focus));
            Assert.AreEqual(ApplicationLifecycleChangeKind.SuspendChanged, suspend.Kind);
            Assert.IsTrue(suspend.Snapshot.HasFocus);
            Assert.IsTrue(suspend.Snapshot.IsSuspended);
            Assert.AreEqual(ApplicationLifecycleChangeKind.FocusChanged, focus.Kind);
            Assert.IsFalse(focus.Snapshot.HasFocus);
            Assert.IsTrue(focus.Snapshot.IsSuspended);
        }

        [Test]
        public void SameFrameSuspendResume_PreservesBothEdges()
        {
            ClaimedBuffer buffer = CreateBuffer(capacity: 2);

            Assert.IsTrue(buffer.ReportSuspend(true));
            Assert.IsTrue(buffer.ReportSuspend(false));

            Assert.IsTrue(buffer.TryDequeue(out ApplicationLifecycleChange suspend));
            Assert.IsTrue(buffer.TryDequeue(out ApplicationLifecycleChange resume));
            Assert.AreEqual(ApplicationLifecycleChangeKind.SuspendChanged, suspend.Kind);
            Assert.IsTrue(suspend.Snapshot.IsSuspended);
            Assert.AreEqual(ApplicationLifecycleChangeKind.SuspendChanged, resume.Kind);
            Assert.IsFalse(resume.Snapshot.IsSuspended);
            Assert.AreEqual(2L, buffer.Current.Revision);
        }

        [Test]
        public void Quitting_IsStickyAndNonVetoNotification()
        {
            ClaimedBuffer buffer = CreateBuffer(capacity: 2);

            Assert.IsTrue(buffer.ReportQuitting());
            Assert.IsFalse(buffer.ReportQuitting());
            Assert.IsTrue(buffer.Current.IsQuitting);

            Assert.IsTrue(buffer.TryDequeue(out ApplicationLifecycleChange change));
            Assert.AreEqual(ApplicationLifecycleChangeKind.Quitting, change.Kind);
            Assert.IsTrue(change.Snapshot.IsQuitting);
            Assert.IsFalse(buffer.TryDequeue(out _));
        }

        [Test]
        public void Overflow_FoldsCompleteRangeIntoSingleStickyResync()
        {
            ClaimedBuffer buffer = CreateBuffer(capacity: 2);

            Assert.IsTrue(buffer.ReportFocus(false));       // revision 1, queued
            Assert.IsTrue(buffer.ReportSuspend(true));      // revision 2, queued
            Assert.IsTrue(buffer.ReportSuspend(false));     // revision 3, overflow
            Assert.IsTrue(buffer.ReportQuitting());         // revision 4, folded while pending

            Assert.AreEqual(4L, buffer.DroppedChangeCount);
            Assert.IsTrue(buffer.TryDequeue(out ApplicationLifecycleChange resync));
            Assert.AreEqual(ApplicationLifecycleChangeKind.StateResyncRequired, resync.Kind);
            Assert.AreEqual(1L, resync.LostRevisionStart);
            Assert.AreEqual(4L, resync.LostRevisionEnd);
            Assert.AreEqual(4L, resync.LostChangeCount);
            Assert.IsTrue(resync.SawSuspendInLostRange);
            Assert.IsTrue(resync.SawQuittingInLostRange);
            Assert.AreEqual(buffer.Current, resync.Snapshot);
            Assert.IsFalse(resync.Snapshot.IsSuspended);
            Assert.IsTrue(resync.Snapshot.IsQuitting);
            Assert.IsFalse(buffer.TryDequeue(out _));

            Assert.IsTrue(buffer.ReportFocus(true));
            Assert.IsTrue(buffer.TryDequeue(out ApplicationLifecycleChange ordinary));
            Assert.AreEqual(ApplicationLifecycleChangeKind.FocusChanged, ordinary.Kind);
            Assert.AreEqual(5L, ordinary.Snapshot.Revision);
            Assert.AreEqual(4L, buffer.DroppedChangeCount);
        }

        [Test]
        public void OverflowCounters_SaturateWithoutWrapping()
        {
            ClaimedBuffer buffer = CreateBuffer(capacity: 1);
            Assert.IsTrue(buffer.ReportFocus(false));

            SetPrivateLong(buffer, "_droppedChangeCount", long.MaxValue - 1);
            Assert.IsTrue(buffer.ReportSuspend(true));
            SetPrivateLong(buffer, "_lostChangeCount", long.MaxValue - 1);
            Assert.IsTrue(buffer.ReportFocus(true));
            Assert.IsTrue(buffer.ReportFocus(false));

            Assert.AreEqual(long.MaxValue, buffer.DroppedChangeCount);
            Assert.IsTrue(buffer.TryDequeue(out ApplicationLifecycleChange resync));
            Assert.AreEqual(long.MaxValue, resync.LostChangeCount);
            Assert.GreaterOrEqual(resync.LostRevisionEnd, resync.LostRevisionStart);
        }

        [Test]
        public void RevisionExhaustion_FoldsQueuedAndSubsequentEffectiveChangesIntoResync()
        {
            ClaimedBuffer buffer = CreateBuffer(capacity: 2);
            Assert.IsTrue(buffer.ReportFocus(false));
            SetPrivateSnapshot(buffer, new ApplicationLifecycleSnapshot(false, false, false, long.MaxValue));

            Assert.IsTrue(buffer.ReportSuspend(true));

            Assert.AreEqual(long.MaxValue, buffer.Current.Revision);
            Assert.IsTrue(buffer.Current.IsSuspended);
            Assert.IsTrue(buffer.TryDequeue(out ApplicationLifecycleChange firstResync));
            Assert.AreEqual(ApplicationLifecycleChangeKind.StateResyncRequired, firstResync.Kind);
            Assert.AreEqual(1L, firstResync.LostRevisionStart);
            Assert.AreEqual(long.MaxValue, firstResync.LostRevisionEnd);
            Assert.AreEqual(2L, firstResync.LostChangeCount);
            Assert.IsTrue(firstResync.SawSuspendInLostRange);
            Assert.IsFalse(buffer.TryDequeue(out _));

            Assert.IsTrue(buffer.ReportFocus(true));
            Assert.IsTrue(buffer.TryDequeue(out ApplicationLifecycleChange exhaustedResync));
            Assert.AreEqual(ApplicationLifecycleChangeKind.StateResyncRequired, exhaustedResync.Kind);
            Assert.AreEqual(long.MaxValue, exhaustedResync.LostRevisionStart);
            Assert.AreEqual(long.MaxValue, exhaustedResync.LostRevisionEnd);
            Assert.AreEqual(1L, exhaustedResync.LostChangeCount);
            Assert.IsTrue(exhaustedResync.Snapshot.HasFocus);
            Assert.IsFalse(buffer.TryDequeue(out _));
        }

        [Test]
        public void ReportAndDrain_WarmLoopAllocatesZeroBytes()
        {
            ClaimedBuffer buffer = CreateBuffer(capacity: 1);
            long checksum = 0;

            for (int i = 0; i < 16; i++)
            {
                buffer.ReportFocus(false);
                buffer.TryDequeue(out _);
                buffer.ReportFocus(true);
                buffer.TryDequeue(out _);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1024; i++)
            {
                buffer.ReportFocus(false);
                buffer.TryDequeue(out ApplicationLifecycleChange first);
                buffer.ReportFocus(true);
                buffer.TryDequeue(out ApplicationLifecycleChange second);
                checksum += first.Snapshot.Revision + second.Snapshot.Revision;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
            Assert.Greater(checksum, 0L);
        }

        [Test]
        public void Constructor_RequiresExplicitRevisionZeroAndPositiveCapacity()
        {
            Assert.Throws<ArgumentException>(() => new ApplicationLifecycleBuffer(
                new ApplicationLifecycleSnapshot(true, false, false, 1),
                1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ApplicationLifecycleBuffer(
                new ApplicationLifecycleSnapshot(true, false, false, 0),
                0));
        }

        [Test]
        public void DrainClaim_IsGrantedExactlyOnce()
        {
            var buffer = new ApplicationLifecycleBuffer(
                new ApplicationLifecycleSnapshot(true, false, false, 0), 1);
            Assert.That(buffer.TryClaimDrain(out IApplicationLifecycleDrainOwner first), Is.True);
            Assert.That(first, Is.Not.Null);
            Assert.That(buffer.TryClaimDrain(out IApplicationLifecycleDrainOwner second), Is.False);
            Assert.That(second, Is.Null);
        }

        private static ClaimedBuffer CreateBuffer(int capacity)
        {
            var buffer = new ApplicationLifecycleBuffer(
                new ApplicationLifecycleSnapshot(
                    hasFocus: true,
                    isSuspended: false,
                    isQuitting: false,
                    revision: 0),
                capacity);
            Assert.That(buffer.TryClaimDrain(out IApplicationLifecycleDrainOwner drain), Is.True);
            return new ClaimedBuffer(buffer, drain);
        }

        private static void SetPrivateLong(
            ClaimedBuffer buffer,
            string fieldName,
            long value)
        {
            FieldInfo field = typeof(ApplicationLifecycleBuffer).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(buffer.Buffer, value);
        }

        private static void SetPrivateSnapshot(
            ClaimedBuffer buffer,
            ApplicationLifecycleSnapshot snapshot)
        {
            FieldInfo field = typeof(ApplicationLifecycleBuffer).GetField(
                "_current",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_current");
            field.SetValue(buffer.Buffer, snapshot);
        }

        private sealed class ClaimedBuffer
        {
            public ClaimedBuffer(ApplicationLifecycleBuffer buffer, IApplicationLifecycleDrainOwner drain)
            {
                Buffer = buffer;
                _drain = drain;
            }
            private readonly IApplicationLifecycleDrainOwner _drain;
            public ApplicationLifecycleBuffer Buffer { get; }
            public ApplicationLifecycleSnapshot Current => _drain.Current;
            public long DroppedChangeCount => _drain.DroppedChangeCount;
            public bool ReportFocus(bool value) => Buffer.ReportFocus(value);
            public bool ReportSuspend(bool value) => Buffer.ReportSuspend(value);
            public bool ReportQuitting() => Buffer.ReportQuitting();
            public bool TryDequeue(out ApplicationLifecycleChange change) => _drain.TryDequeue(out change);
        }
    }
}
