using System;
using MxFramework.Platform;
using NUnit.Framework;

namespace MxFramework.Tests.Platform
{
    public sealed class WallClockTests
    {
        [Test]
        public void SystemClock_ReturnsUtc()
        {
            var clock = new SystemUtcWallClock();

            Assert.AreEqual(DateTimeKind.Utc, clock.UtcNow.Kind);
            Assert.IsTrue(PlatformUtcClock.TryReadUtc(clock, out DateTime value, out PlatformError error));
            Assert.AreEqual(DateTimeKind.Utc, value.Kind);
            Assert.IsTrue(error.IsNone);
        }

        [Test]
        public void DeterministicClock_CanMoveBackwardWithoutNormalization()
        {
            DateTime later = new DateTime(638000000000000000L, DateTimeKind.Utc);
            DateTime earlier = later.AddMinutes(-5);
            var clock = new SequenceWallClock(later, earlier);

            Assert.IsTrue(PlatformUtcClock.TryReadUtc(clock, out DateTime first, out _));
            Assert.IsTrue(PlatformUtcClock.TryReadUtc(clock, out DateTime second, out _));
            Assert.AreEqual(later, first);
            Assert.AreEqual(earlier, second);
            Assert.Less(second, first);
        }

        [TestCase(DateTimeKind.Local)]
        [TestCase(DateTimeKind.Unspecified)]
        public void InvalidKind_FailsClosedWithoutPublishingValue(DateTimeKind kind)
        {
            var clock = new FixedWallClock(new DateTime(638000000000000000L, kind));

            bool success = PlatformUtcClock.TryReadUtc(
                clock,
                out DateTime value,
                out PlatformError error);

            Assert.IsFalse(success);
            Assert.AreEqual(default(DateTime), value);
            Assert.AreEqual(PlatformErrorCode.AdapterFailure, error.Code);
        }

        [Test]
        public void ProviderException_FailsStructurallyWithoutPublishingValue()
        {
            bool success = PlatformUtcClock.TryReadUtc(
                new ThrowingWallClock(),
                out DateTime value,
                out PlatformError error);

            Assert.IsFalse(success);
            Assert.AreEqual(default(DateTime), value);
            Assert.AreEqual(PlatformErrorCode.AdapterFailure, error.Code);
            Assert.IsInstanceOf<InvalidOperationException>(error.Exception);
        }

        [Test]
        public void NullProvider_FailsAsInvalidArgument()
        {
            Assert.IsFalse(PlatformUtcClock.TryReadUtc(null, out _, out PlatformError error));
            Assert.AreEqual(PlatformErrorCode.InvalidArgument, error.Code);
        }

        [Test]
        public void SystemClockGetter_WarmLoopAllocatesZeroBytes()
        {
            var clock = new SystemUtcWallClock();
            long checksum = 0;

            _ = clock.UtcNow;
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1024; i++)
                checksum ^= clock.UtcNow.Ticks;

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
            Assert.AreNotEqual(long.MinValue, checksum);
        }

        private sealed class FixedWallClock : IWallClock
        {
            public FixedWallClock(DateTime value)
            {
                UtcNow = value;
            }

            public DateTime UtcNow { get; }
        }

        private sealed class SequenceWallClock : IWallClock
        {
            private readonly DateTime[] _values;
            private int _index;

            public SequenceWallClock(params DateTime[] values)
            {
                _values = values;
            }

            public DateTime UtcNow
            {
                get
                {
                    int index = _index < _values.Length ? _index : _values.Length - 1;
                    _index++;
                    return _values[index];
                }
            }
        }

        private sealed class ThrowingWallClock : IWallClock
        {
            public DateTime UtcNow => throw new InvalidOperationException("clock failure");
        }
    }
}
