using System;
using MxFramework.Combat.Core;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Core
{
    public sealed class CombatFixedStepDriverTests
    {
        [Test]
        public void Advance_BelowFixedDelta_CreatesZeroStepBatch()
        {
            var driver = new CombatFixedStepDriver(new CombatStepConfig(10, 4));

            CombatFixedStepBatch batch = driver.Advance(runtimeFrameIndex: 0, runtimeDeltaTime: 0.05d);

            Assert.IsFalse(batch.HasSteps);
            Assert.AreEqual(0, batch.StepCount);
            Assert.AreEqual(CombatFrame.Zero, batch.StartFrame);
            Assert.AreEqual(CombatFrame.Zero, batch.EndFrame);
            Assert.AreEqual(CombatFrame.Zero, driver.CurrentFrame);
            Assert.AreEqual(0.05d, batch.RemainingTime, 0.0000001d);
            Assert.IsFalse(batch.MaxStepLimitReached);
        }

        [Test]
        public void Advance_OneFixedDelta_CreatesOneStepBatch()
        {
            var driver = new CombatFixedStepDriver(new CombatStepConfig(10, 4));

            CombatFixedStepBatch batch = driver.Advance(runtimeFrameIndex: 0, runtimeDeltaTime: 0.1d);

            Assert.IsTrue(batch.HasSteps);
            Assert.AreEqual(1, batch.StepCount);
            Assert.AreEqual(new CombatFrame(1), batch.GetStepFrame(0));
            Assert.AreEqual(new CombatFrame(1), batch.EndFrame);
            Assert.AreEqual(new CombatFrame(1), driver.CurrentFrame);
            Assert.AreEqual(0d, batch.RemainingTime, 0.0000001d);
        }

        [Test]
        public void Advance_MultipleFixedDeltas_CreatesDeterministicStepFrames()
        {
            var driver = new CombatFixedStepDriver(new CombatStepConfig(10, 8));

            CombatFixedStepBatch batch = driver.Advance(runtimeFrameIndex: 0, runtimeDeltaTime: 0.35d);

            Assert.AreEqual(3, batch.StepCount);
            Assert.AreEqual(new CombatFrame(1), batch.GetStepFrame(0));
            Assert.AreEqual(new CombatFrame(2), batch.GetStepFrame(1));
            Assert.AreEqual(new CombatFrame(3), batch.GetStepFrame(2));
            Assert.AreEqual(new CombatFrame(3), batch.EndFrame);
            Assert.AreEqual(0.05d, batch.RemainingTime, 0.0000001d);
            Assert.IsFalse(batch.MaxStepLimitReached);
        }

        [Test]
        public void Advance_RespectsMaxStepsPerUpdateAndRetainsAccumulator()
        {
            var driver = new CombatFixedStepDriver(new CombatStepConfig(10, 2));

            CombatFixedStepBatch capped = driver.Advance(runtimeFrameIndex: 0, runtimeDeltaTime: 0.35d);
            CombatFixedStepBatch catchUp = driver.Advance(runtimeFrameIndex: 1, runtimeDeltaTime: 0d);

            Assert.AreEqual(2, capped.StepCount);
            Assert.AreEqual(new CombatFrame(2), capped.EndFrame);
            Assert.AreEqual(0.15d, capped.RemainingTime, 0.0000001d);
            Assert.IsTrue(capped.MaxStepLimitReached);

            Assert.AreEqual(1, catchUp.StepCount);
            Assert.AreEqual(new CombatFrame(3), catchUp.EndFrame);
            Assert.AreEqual(0.05d, catchUp.RemainingTime, 0.0000001d);
            Assert.IsFalse(catchUp.MaxStepLimitReached);
        }

        [Test]
        public void Advance_SameRuntimeFrame_ReturnsCachedBatchWithoutDoubleStepping()
        {
            var driver = new CombatFixedStepDriver(new CombatStepConfig(10, 4));

            CombatFixedStepBatch first = driver.Advance(runtimeFrameIndex: 7, runtimeDeltaTime: 0.1d);
            CombatFixedStepBatch second = driver.Advance(runtimeFrameIndex: 7, runtimeDeltaTime: 0.1d);

            Assert.AreEqual(1, first.StepCount);
            Assert.AreEqual(1, second.StepCount);
            Assert.AreEqual(new CombatFrame(1), driver.CurrentFrame);
            Assert.AreEqual(first.EndFrame, second.EndFrame);
            Assert.AreEqual(first.RemainingTime, second.RemainingTime);
        }

        [Test]
        public void GetStepFrame_OutsideBatch_Throws()
        {
            var driver = new CombatFixedStepDriver(new CombatStepConfig(10, 4));
            CombatFixedStepBatch batch = driver.Advance(runtimeFrameIndex: 0, runtimeDeltaTime: 0.1d);

            Assert.Throws<ArgumentOutOfRangeException>(() => batch.GetStepFrame(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => batch.GetStepFrame(1));
        }
    }
}
