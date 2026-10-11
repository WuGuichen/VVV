using System;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeCooldownTrackerTests
    {
        [Test]
        public void NewCooldown_IsReadyWithNoRemainingFrames()
        {
            var cooldowns = new CooldownTracker();

            Assert.IsTrue(cooldowns.IsReady(10, RuntimeFrame.Zero));
            Assert.AreEqual(0L, cooldowns.GetRemainingFrames(10, RuntimeFrame.Zero));
        }

        [Test]
        public void Start_MarksActiveUntilEndFrame()
        {
            var cooldowns = new CooldownTracker();

            cooldowns.Start(10, new RuntimeFrame(5), 3);

            Assert.IsFalse(cooldowns.IsReady(10, new RuntimeFrame(5)));
            Assert.IsFalse(cooldowns.IsReady(10, new RuntimeFrame(7)));
            Assert.IsTrue(cooldowns.IsReady(10, new RuntimeFrame(8)));
        }

        [Test]
        public void GetRemainingFrames_ClampsAtZero()
        {
            var cooldowns = new CooldownTracker();

            cooldowns.Start(10, new RuntimeFrame(5), 3);

            Assert.AreEqual(3L, cooldowns.GetRemainingFrames(10, new RuntimeFrame(5)));
            Assert.AreEqual(1L, cooldowns.GetRemainingFrames(10, new RuntimeFrame(7)));
            Assert.AreEqual(0L, cooldowns.GetRemainingFrames(10, new RuntimeFrame(8)));
            Assert.AreEqual(0L, cooldowns.GetRemainingFrames(10, new RuntimeFrame(12)));
        }

        [Test]
        public void TryConsume_StartsCooldownOnlyWhenReady()
        {
            var cooldowns = new CooldownTracker();

            Assert.IsTrue(cooldowns.TryConsume(10, new RuntimeFrame(2), 4));
            Assert.AreEqual(4L, cooldowns.GetRemainingFrames(10, new RuntimeFrame(2)));
            Assert.IsFalse(cooldowns.TryConsume(10, new RuntimeFrame(3), 9));
            Assert.AreEqual(3L, cooldowns.GetRemainingFrames(10, new RuntimeFrame(3)));

            Assert.IsTrue(cooldowns.TryConsume(10, new RuntimeFrame(6), 2));
            Assert.AreEqual(2L, cooldowns.GetRemainingFrames(10, new RuntimeFrame(6)));
        }

        [Test]
        public void ZeroDuration_IsImmediatelyReady()
        {
            var cooldowns = new CooldownTracker();

            cooldowns.Start(10, new RuntimeFrame(5), 0);

            Assert.IsTrue(cooldowns.IsReady(10, new RuntimeFrame(5)));
            Assert.AreEqual(0L, cooldowns.GetRemainingFrames(10, new RuntimeFrame(5)));
            Assert.IsTrue(cooldowns.TryConsume(10, new RuntimeFrame(5), 0));
        }

        [Test]
        public void NegativeDuration_Throws()
        {
            var cooldowns = new CooldownTracker();

            Assert.Throws<ArgumentOutOfRangeException>(() => cooldowns.Start(10, RuntimeFrame.Zero, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => cooldowns.TryConsume(10, RuntimeFrame.Zero, -1));
        }

        [Test]
        public void EndFrameOverflow_Throws()
        {
            var cooldowns = new CooldownTracker();

            Assert.Throws<ArgumentOutOfRangeException>(() => cooldowns.Start(10, new RuntimeFrame(long.MaxValue), 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => cooldowns.TryConsume(10, new RuntimeFrame(long.MaxValue), 1));
        }

        [Test]
        public void Clear_RemovesCooldowns()
        {
            var cooldowns = new CooldownTracker();
            cooldowns.Start(10, RuntimeFrame.Zero, 3);
            cooldowns.Start(20, RuntimeFrame.Zero, 7);

            cooldowns.Clear();

            Assert.IsTrue(cooldowns.IsReady(10, RuntimeFrame.Zero));
            Assert.IsTrue(cooldowns.IsReady(20, RuntimeFrame.Zero));
            Assert.AreEqual(0, cooldowns.CreateSnapshot().Entries.Count);
        }

        [Test]
        public void Remove_DeletesSingleCooldown()
        {
            var cooldowns = new CooldownTracker();
            cooldowns.Start(10, RuntimeFrame.Zero, 3);
            cooldowns.Start(20, RuntimeFrame.Zero, 3);

            Assert.IsTrue(cooldowns.Remove(10));
            Assert.IsFalse(cooldowns.Remove(10));

            Assert.IsTrue(cooldowns.IsReady(10, RuntimeFrame.Zero));
            Assert.IsFalse(cooldowns.IsReady(20, RuntimeFrame.Zero));
        }

        [Test]
        public void CleanupExpired_RemovesExpiredCooldowns()
        {
            var cooldowns = new CooldownTracker();
            cooldowns.Start(10, RuntimeFrame.Zero, 2);
            cooldowns.Start(20, RuntimeFrame.Zero, 4);
            cooldowns.Start(30, RuntimeFrame.Zero, 6);

            Assert.AreEqual(2, cooldowns.CleanupExpired(new RuntimeFrame(4)));

            CooldownTrackerSnapshot snapshot = cooldowns.CreateSnapshot();
            Assert.AreEqual(1, snapshot.Entries.Count);
            Assert.AreEqual(30, snapshot.Entries[0].Id);
        }

        [Test]
        public void CreateSnapshotWithFrame_ExcludesExpiredByDefault()
        {
            var cooldowns = new CooldownTracker();
            cooldowns.Start(10, RuntimeFrame.Zero, 2);
            cooldowns.Start(20, RuntimeFrame.Zero, 4);

            CooldownTrackerSnapshot active = cooldowns.CreateSnapshot(new RuntimeFrame(2));
            CooldownTrackerSnapshot all = cooldowns.CreateSnapshot(new RuntimeFrame(2), includeExpired: true);

            Assert.AreEqual(1, active.Entries.Count);
            Assert.AreEqual(20, active.Entries[0].Id);
            Assert.AreEqual(2, all.Entries.Count);
        }

        [Test]
        public void CreateSnapshot_ReturnsSortedEndFrames()
        {
            var cooldowns = new CooldownTracker();
            cooldowns.Start(20, new RuntimeFrame(1), 4);
            cooldowns.Start(10, new RuntimeFrame(2), 3);

            CooldownTrackerSnapshot snapshot = cooldowns.CreateSnapshot();

            Assert.AreEqual(2, snapshot.Entries.Count);
            Assert.AreEqual(10, snapshot.Entries[0].Id);
            Assert.AreEqual(new RuntimeFrame(5), snapshot.Entries[0].EndFrame);
            Assert.AreEqual(20, snapshot.Entries[1].Id);
            Assert.AreEqual(new RuntimeFrame(5), snapshot.Entries[1].EndFrame);
        }
    }
}
