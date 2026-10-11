using System;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeRateLimiterTests
    {
        [Test]
        public void AllowFrame_FirstCallAllows()
        {
            var limiter = new RuntimeRateLimiter();

            Assert.IsTrue(limiter.AllowFrame(10, RuntimeFrame.Zero, 3));
        }

        [Test]
        public void AllowFrame_BlocksBeforeIntervalAndAllowsAtInterval()
        {
            var limiter = new RuntimeRateLimiter();

            Assert.IsTrue(limiter.AllowFrame(10, new RuntimeFrame(5), 3));
            Assert.IsFalse(limiter.AllowFrame(10, new RuntimeFrame(7), 3));
            Assert.IsTrue(limiter.AllowFrame(10, new RuntimeFrame(8), 3));
        }

        [Test]
        public void AllowFrame_TracksIdsIndependently()
        {
            var limiter = new RuntimeRateLimiter();

            Assert.IsTrue(limiter.AllowFrame(10, RuntimeFrame.Zero, 5));
            Assert.IsTrue(limiter.AllowFrame(20, new RuntimeFrame(1), 5));

            Assert.IsFalse(limiter.AllowFrame(10, new RuntimeFrame(4), 5));
            Assert.IsFalse(limiter.AllowFrame(20, new RuntimeFrame(5), 5));
            Assert.IsTrue(limiter.AllowFrame(10, new RuntimeFrame(5), 5));
            Assert.IsTrue(limiter.AllowFrame(20, new RuntimeFrame(6), 5));
        }

        [Test]
        public void AllowFrame_ZeroIntervalAlwaysAllows()
        {
            var limiter = new RuntimeRateLimiter();

            Assert.IsTrue(limiter.AllowFrame(10, RuntimeFrame.Zero, 0));
            Assert.IsTrue(limiter.AllowFrame(10, RuntimeFrame.Zero, 0));
        }

        [Test]
        public void AllowFrame_ResetAndClearRemoveHistory()
        {
            var limiter = new RuntimeRateLimiter();

            Assert.IsTrue(limiter.AllowFrame(10, RuntimeFrame.Zero, 5));
            Assert.IsFalse(limiter.AllowFrame(10, new RuntimeFrame(1), 5));

            limiter.Reset(10);

            Assert.IsTrue(limiter.AllowFrame(10, new RuntimeFrame(1), 5));
            Assert.IsTrue(limiter.AllowFrame(20, RuntimeFrame.Zero, 5));

            limiter.Clear();

            Assert.IsTrue(limiter.AllowFrame(10, new RuntimeFrame(2), 5));
            Assert.IsTrue(limiter.AllowFrame(20, new RuntimeFrame(1), 5));
        }

        [Test]
        public void AllowFrame_NegativeIntervalThrows()
        {
            var limiter = new RuntimeRateLimiter();

            Assert.Throws<ArgumentOutOfRangeException>(() => limiter.AllowFrame(10, RuntimeFrame.Zero, -1));
        }

        [Test]
        public void AllowSeconds_UsesExplicitElapsedSeconds()
        {
            var limiter = new RuntimeRateLimiter();

            Assert.IsTrue(limiter.AllowSeconds(10, 1.0d, 0.5d));
            Assert.IsFalse(limiter.AllowSeconds(10, 1.49d, 0.5d));
            Assert.IsTrue(limiter.AllowSeconds(10, 1.5d, 0.5d));
        }

        [Test]
        public void AllowSeconds_TracksIdsIndependentlyAndZeroIntervalAllows()
        {
            var limiter = new RuntimeRateLimiter();

            Assert.IsTrue(limiter.AllowSeconds(10, 0d, 1d));
            Assert.IsTrue(limiter.AllowSeconds(20, 0.5d, 1d));
            Assert.IsFalse(limiter.AllowSeconds(10, 0.75d, 1d));
            Assert.IsTrue(limiter.AllowSeconds(20, 1.5d, 1d));
            Assert.IsTrue(limiter.AllowSeconds(10, 0.75d, 0d));
            Assert.IsTrue(limiter.AllowSeconds(10, 0.75d, 0d));
        }

        [Test]
        public void AllowSeconds_InvalidValuesThrow()
        {
            var limiter = new RuntimeRateLimiter();

            Assert.Throws<ArgumentOutOfRangeException>(() => limiter.AllowSeconds(10, 0d, -0.1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => limiter.AllowSeconds(10, -0.1d, 0.1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => limiter.AllowSeconds(10, double.NaN, 0.1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => limiter.AllowSeconds(10, 0d, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => limiter.AllowSeconds(10, double.PositiveInfinity, 0.1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => limiter.AllowSeconds(10, 0d, double.PositiveInfinity));
        }

        [Test]
        public void DebouncerFrame_WaitsUntilNoMarksForInterval()
        {
            var debouncer = new RuntimeDebouncer();

            debouncer.MarkFrame(10, new RuntimeFrame(5));

            Assert.IsFalse(debouncer.IsReadyFrame(10, new RuntimeFrame(7), 3));

            debouncer.MarkFrame(10, new RuntimeFrame(7));

            Assert.IsFalse(debouncer.IsReadyFrame(10, new RuntimeFrame(9), 3));
            Assert.IsTrue(debouncer.IsReadyFrame(10, new RuntimeFrame(10), 3));
        }

        [Test]
        public void DebouncerFrame_ConsumeReadyRemovesMark()
        {
            var debouncer = new RuntimeDebouncer();

            debouncer.MarkFrame(10, RuntimeFrame.Zero);

            Assert.IsFalse(debouncer.ConsumeReadyFrame(10, new RuntimeFrame(2), 3));
            Assert.IsTrue(debouncer.ConsumeReadyFrame(10, new RuntimeFrame(3), 3));
            Assert.IsFalse(debouncer.IsReadyFrame(10, new RuntimeFrame(4), 0));
        }

        [Test]
        public void DebouncerFrame_TracksIdsIndependentlyAndSupportsZeroInterval()
        {
            var debouncer = new RuntimeDebouncer();

            debouncer.MarkFrame(10, new RuntimeFrame(4));
            debouncer.MarkFrame(20, new RuntimeFrame(6));

            Assert.IsTrue(debouncer.IsReadyFrame(10, new RuntimeFrame(4), 0));
            Assert.IsFalse(debouncer.IsReadyFrame(20, new RuntimeFrame(7), 2));
            Assert.IsTrue(debouncer.IsReadyFrame(20, new RuntimeFrame(8), 2));
        }

        [Test]
        public void DebouncerFrame_NegativeIntervalThrows()
        {
            var debouncer = new RuntimeDebouncer();
            debouncer.MarkFrame(10, RuntimeFrame.Zero);

            Assert.Throws<ArgumentOutOfRangeException>(() => debouncer.IsReadyFrame(10, RuntimeFrame.Zero, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => debouncer.ConsumeReadyFrame(10, RuntimeFrame.Zero, -1));
        }

        [Test]
        public void DebouncerSeconds_UsesExplicitElapsedSeconds()
        {
            var debouncer = new RuntimeDebouncer();

            debouncer.MarkSeconds(10, 1.0d);
            Assert.IsFalse(debouncer.IsReadySeconds(10, 1.49d, 0.5d));

            debouncer.MarkSeconds(10, 1.25d);
            Assert.IsFalse(debouncer.ConsumeReadySeconds(10, 1.5d, 0.5d));
            Assert.IsTrue(debouncer.ConsumeReadySeconds(10, 1.75d, 0.5d));
            Assert.IsFalse(debouncer.IsReadySeconds(10, 2.0d, 0d));
        }

        [Test]
        public void DebouncerSeconds_InvalidValuesThrow()
        {
            var debouncer = new RuntimeDebouncer();

            Assert.Throws<ArgumentOutOfRangeException>(() => debouncer.MarkSeconds(10, -0.1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => debouncer.MarkSeconds(10, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => debouncer.MarkSeconds(10, double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => debouncer.IsReadySeconds(10, 0d, -0.1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => debouncer.ConsumeReadySeconds(10, 0d, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => debouncer.ConsumeReadySeconds(10, 0d, double.PositiveInfinity));
        }
    }
}
