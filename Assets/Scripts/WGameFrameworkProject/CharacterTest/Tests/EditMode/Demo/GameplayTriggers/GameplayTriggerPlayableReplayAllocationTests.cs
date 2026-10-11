using System;
using MxFramework.Demo.GameplayTriggers;
using NUnit.Framework;

namespace MxFramework.Tests.Demo.GameplayTriggers
{
    public sealed class GameplayTriggerPlayableReplayAllocationTests
    {
        [Test]
        public void ExogenousInputTrace_ReplaysCanonicalHashGolden()
        {
            int[] sampleFrames = { 0, 15, 35, 60, 90, 109 };
            long[] golden =
            {
                -7273545074373273816L,
                -4332799032723848891L,
                -8704691914066867410L,
                -2121673639508686452L,
                -1787622776846822932L,
                5068889283529941476L
            };
            long[] first = RunTrace(sampleFrames);
            long[] second = RunTrace(sampleFrames);

            CollectionAssert.AreEqual(golden, first);
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void CaptureFreshRestore_ReplaysCanonicalHashSuffix()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                for (int frame = 0; frame < 72; frame++)
                    session.Tick(TraceInput(frame));
                Assert.That(session.CaptureCheckpoint().Success, Is.True);

                var baseline = new long[24];
                for (int frame = 0; frame < baseline.Length; frame++)
                {
                    session.Tick(SuffixInput(frame));
                    baseline[frame] = session.ComputeHash();
                }

                Assert.That(session.RestoreFresh().Success, Is.True);
                for (int frame = 0; frame < baseline.Length; frame++)
                {
                    session.Tick(SuffixInput(frame));
                    Assert.That(
                        session.ComputeHash(),
                        Is.EqualTo(baseline[frame]),
                        "suffix frame " + frame);
                }
            }
        }

        [Test]
        public void PreAdmissionInputLatch_IsExcludedFromAuthorityHash()
        {
            using (var first = new GameplayTriggerPlayableSession())
            using (var second = new GameplayTriggerPlayableSession())
            {
                for (int frame = 0; frame < 20; frame++)
                {
                    first.Tick(TraceInput(frame));
                    second.Tick(TraceInput(frame));
                }
                Assert.That(first.SubmitInput(new GameplayTriggerPlayableInput(
                    moveX: -1000,
                    moveZ: 1000,
                    capture: true,
                    restore: true)), Is.True);

                Assert.That(first.ComputeHash(), Is.EqualTo(second.ComputeHash()));
            }
        }

        [Test]
        public void WarmAuthorityPath_SixtyFourLogicalFrames_AllocatesZeroBytes()
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                Assert.That(session.SubmitInput(new GameplayTriggerPlayableInput(
                    moveX: 1000,
                    moveZ: 0)), Is.True);
                for (int frame = 0; frame < 400; frame++)
                    session.Tick();

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int frame = 0; frame < 64; frame++)
                    session.Tick();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(allocated, Is.Zero);
            }
        }

        private static long[] RunTrace(int[] sampleFrames)
        {
            using (var session = new GameplayTriggerPlayableSession())
            {
                var hashes = new long[sampleFrames.Length];
                int sampleIndex = 0;
                for (int frame = 0; frame < 110; frame++)
                {
                    session.Tick(TraceInput(frame));
                    if (sampleIndex < sampleFrames.Length &&
                        sampleFrames[sampleIndex] == frame)
                    {
                        hashes[sampleIndex] = session.ComputeHash();
                        sampleIndex++;
                    }
                }
                return hashes;
            }
        }

        private static GameplayTriggerPlayableInput TraceInput(int frame)
        {
            if (frame < 40)
                return new GameplayTriggerPlayableInput(1000, 0);
            if (frame < 50)
                return new GameplayTriggerPlayableInput(0, 0);
            if (frame < 90)
                return new GameplayTriggerPlayableInput(1000, 0);
            return new GameplayTriggerPlayableInput(-500, 0);
        }

        private static GameplayTriggerPlayableInput SuffixInput(int frame)
        {
            int phase = frame % 6;
            if (phase < 2)
                return new GameplayTriggerPlayableInput(0, 0);
            if (phase < 4)
                return new GameplayTriggerPlayableInput(-1000, 0);
            return new GameplayTriggerPlayableInput(1000, 0);
        }
    }
}
