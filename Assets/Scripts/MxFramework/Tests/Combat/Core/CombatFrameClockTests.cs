using System;
using MxFramework.Combat.Core;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Core
{
    public class CombatFrameClockTests
    {
        [Test]
        public void Step_AdvancesOneFrame()
        {
            var clock = new CombatFrameClock();

            CombatFrame frame = clock.Step();

            Assert.AreEqual(1, frame.Value);
            Assert.AreEqual(1, clock.CurrentFrame.Value);
            Assert.AreEqual(1, clock.StepCount);
        }

        [Test]
        public void StepMany_AdvancesDeterministically()
        {
            var first = new CombatFrameClock();
            var second = new CombatFrameClock();

            first.Step(12);
            for (int i = 0; i < 12; i++)
            {
                second.Step();
            }

            Assert.AreEqual(first.CurrentFrame, second.CurrentFrame);
            Assert.AreEqual(12, first.StepCount);
        }

        [Test]
        public void Reset_ReturnsToZero()
        {
            var clock = new CombatFrameClock();
            clock.Step(8);

            clock.Reset();

            Assert.AreEqual(CombatFrame.Zero, clock.CurrentFrame);
            Assert.AreEqual(0, clock.StepCount);
        }

        [Test]
        public void NegativeFrameOrStep_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatFrame(-1));

            var clock = new CombatFrameClock();
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Step(-1));
        }

        [Test]
        public void InvalidStepConfig_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatStepConfig(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatStepConfig(60, 0));
        }
    }
}
